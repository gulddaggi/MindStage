package com.example.mindstagewatch

import android.Manifest
import android.content.*
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.os.IBinder
import android.provider.Settings
import android.util.Log
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import com.example.mindstagewatch.net.Api
import com.example.mindstagewatch.net.TokenCommitRequest
import com.example.mindstagewatch.hr.HrCaptureService
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.delay
import com.example.mindstagewatch.fcm.MyFirebaseMessagingService

// 1. 서비스 인터랙션을 위한 콜백 인터페이스 구현
class MainActivity : ComponentActivity(), HrCaptureService.ServiceCallback {

    private lateinit var etInterviewId: EditText
    private lateinit var btnSave: Button
    private lateinit var btnStart: Button
    private lateinit var btnStop: Button
    private lateinit var tvStatus: TextView
    private lateinit var tvBpm: TextView

    private var hrService: HrCaptureService? = null
    private var isBound = false
    private var pendingStartId: Long = -1L
    private var pendingDurationSec: Long = -1L

    // 2. 서비스 연결자
    private val connection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName, service: IBinder) {
            val binder = service as HrCaptureService.LocalBinder
            hrService = binder.getService()
            hrService?.callback = this@MainActivity
            isBound = true
        }

        override fun onServiceDisconnected(name: ComponentName) {
            isBound = false
            hrService?.callback = null
            hrService = null
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        // 프로덕션 서버
        Api.setBaseUrl("https://mindstage.duckdns.org")

        // UI 바인딩
        etInterviewId = findViewById(R.id.etInterviewId)
        btnSave = findViewById(R.id.btnSave)
        btnStart = findViewById(R.id.btnStart)
        btnStop = findViewById(R.id.btnStop)
        tvStatus = findViewById(R.id.tvStatus)
        tvBpm = findViewById(R.id.tvBpm)

        // 권한 미리 확보 (Android 10+/13+ 권장)
        ensureSensorsPermission()

        // 장치 UUID 로그
        val uuid = deviceUuid()
        Log.d("MainActivity", "Device UUID: $uuid")
        toast("$uuid")

        // HMAC용 provider 등록
        Api.setUuidProvider { deviceUuid() }
        Api.setDeviceSecretProvider { prefs.getString(KEY_DEVICE_SECRET, null) }

        // 앱 시작 시 항상 FCM 토큰을 서버에 전송
        FirebaseMessaging.getInstance().token.addOnCompleteListener { task ->
            if (task.isSuccessful) {
                val t = task.result
                if (!t.isNullOrBlank()) {
                    CoroutineScope(Dispatchers.IO).launch {
                        try {
                            val res = Api.galaxy.commitFcmToken(
                                TokenCommitRequest(uuid = deviceUuid(), token = t)
                            )
                            // 성공 시 토큰/시크릿 저장
                            prefs.edit().putString(KEY_FCM_TOKEN, t).apply()
                            prefs.edit().putLong(KEY_LAST_COMMIT_AT, System.currentTimeMillis()).apply()
                            res.data?.deviceSecret?.let { secret ->
                                prefs.edit().putString(KEY_DEVICE_SECRET, secret).apply()
                                Api.setDeviceSecretProvider { secret }
                                Log.d("MainActivity", "Device secret bootstrapped on app start")
                            }
                            Log.d("MainActivity", "FCM token sent to server on app start")
                        } catch (e: Exception) {
                            Log.e("MainActivity", "Failed to send FCM token on app start", e)
                        }
                    }
                }
            }
        }

        updateUi(isMeasuring = false)
        etInterviewId.setText(prefs.getString(KEY_INTERVIEW_ID, "") ?: "")

        btnSave.setOnClickListener {
            val idText = etInterviewId.text.toString().trim()
            if (idText.isEmpty()) {
                toast("Interview ID를 입력하세요.")
                return@setOnClickListener
            }
            prefs.edit().putString(KEY_INTERVIEW_ID, idText).apply()
            toast("Interview ID 저장됨: $idText")
        }

        btnStart.setOnClickListener {
            if (!ensureSensorsPermission()) return@setOnClickListener
            val idText = prefs.getString(KEY_INTERVIEW_ID, "") ?: ""
            if (idText.isBlank()) {
                toast("먼저 Interview ID를 저장하세요.")
                return@setOnClickListener
            }
            val interviewId = idText.toLongOrNull()
            if (interviewId == null) {
                toast("Interview ID는 숫자여야 합니다.")
                return@setOnClickListener
            }
            startMeasurement(interviewId, 20L * 60L)
        }

        btnStop.setOnClickListener {
            // 바인딩 해제 후 종료 지시
            if (isBound) {
                unbindService(connection)
                isBound = false
                hrService?.callback = null
            }
            val i = Intent(this, HrCaptureService::class.java).apply {
                action = HrCaptureService.ACTION_STOP_AND_POST
            }
            try {
                ContextCompat.startForegroundService(this, i)
            } catch (e: Exception) {
                Log.e("MainActivity", "Failed to stop service", e)
            }
            updateUi(isMeasuring = false)
            tvStatus.text = "[측정 종료 요청…]"
        }

        // FCM 알림 클릭으로 들어온 경우 처리 (자동 시작/종료)
        handlePushIntent(intent)
    }

    override fun onNewIntent(intent: Intent?) {
        super.onNewIntent(intent)
        setIntent(intent)
        handlePushIntent(intent)
    }

    private fun handlePushIntent(i: Intent?) {
        if (i == null) return
        // 종료 요청 알림
        if (i.getBooleanExtra(MyFirebaseMessagingService.EXTRA_STOP_REQUEST, false)) {
            val stop = Intent(this, HrCaptureService::class.java).apply {
                action = HrCaptureService.ACTION_STOP_AND_POST
            }
            try {
                ContextCompat.startForegroundService(this, stop)
            } catch (e: Exception) {
                Log.e("MainActivity", "Failed to stop service from push", e)
            }
            tvStatus.text = "[측정 종료 요청…]"
            return
        }
        // 시작 요청 알림: 인터뷰 ID와 duration 전달
        val startId = i.getLongExtra(MyFirebaseMessagingService.EXTRA_START_INTERVIEW_ID, -1L)
        val durationSec = i.getLongExtra(MyFirebaseMessagingService.EXTRA_START_DURATION_SEC, 20L * 60L)
        if (startId > 0L) {
            etInterviewId.setText(startId.toString())
            prefs.edit().putString(KEY_INTERVIEW_ID, startId.toString()).apply()
            if (ensureSensorsPermission()) {
                startMeasurement(startId, durationSec)
            } else {
                // 권한 허용 후 자동 시작
                pendingStartId = startId
                pendingDurationSec = durationSec
                toast("권한 허용 후 자동으로 측정을 시작합니다.")
            }
        }
    }

    private fun startMeasurement(interviewId: Long, durationSec: Long) {
        val i = Intent(this, HrCaptureService::class.java).apply {
            action = HrCaptureService.ACTION_START
            putExtra("interviewId", interviewId)
            putExtra("deviceUuid", deviceUuid())
        }
        try {
            ContextCompat.startForegroundService(this, i)
            bindService(i, connection, Context.BIND_AUTO_CREATE)
            updateUi(isMeasuring = true)
            tvStatus.text = "[푸시로 측정 시작] interviewId=$interviewId"
            // duration 후 자동 종료 트리거
            CoroutineScope(Dispatchers.Main).launch {
                delay(durationSec * 1000L)
                val stop = Intent(this@MainActivity, HrCaptureService::class.java).apply {
                    action = HrCaptureService.ACTION_STOP_AND_POST
                }
                try {
                    // Android 12+ 백그라운드 서비스 시작 제한 대응
                    ContextCompat.startForegroundService(this@MainActivity, stop)
                } catch (e: Exception) {
                    Log.e("MainActivity", "Failed to stop service automatically", e)
                    // 실패 시 알림으로 사용자에게 종료 요청
                    tvStatus.text = "[측정 시간 종료 - 수동 종료 필요]"
                }
            }
        } catch (e: Exception) {
            Log.e("MainActivity", "startForegroundService failed", e)
        }
    }

    override fun onStart() {
        super.onStart()
        // 서비스와 바인딩 (상태 업데이트 수신)
        bindService(Intent(this, HrCaptureService::class.java), connection, Context.BIND_AUTO_CREATE)
    }

    override fun onStop() {
        super.onStop()
        if (isBound) {
            unbindService(connection)
            isBound = false
            hrService?.callback = null
        }
    }

    // --- 콜백 구현 ---
    override fun onBpmChanged(bpm: Int) {
        runOnUiThread { if (bpm >= 0) tvBpm.text = "$bpm bpm" }
    }

    override fun onStatusChanged(msg: String) {
        runOnUiThread {
            tvStatus.text = msg
            if (msg.contains("시작")) {
                updateUi(isMeasuring = true)
            } else if (msg.contains("오류") || msg.contains("종료")) {
                updateUi(isMeasuring = false)
            }
        }
    }

    override fun onPostDone(ok: Boolean, msg: String) {
        runOnUiThread {
            tvStatus.text = if (ok) "[전송완료] $msg" else "[전송실패] $msg"
            updateUi(isMeasuring = false)
        }
    }

    private fun updateUi(isMeasuring: Boolean) {
        btnStart.isEnabled = !isMeasuring
        btnStop.isEnabled = isMeasuring
        etInterviewId.isEnabled = !isMeasuring
        btnSave.isEnabled = !isMeasuring
        if (isMeasuring) {
            tvStatus.text = "[지속 측정 중]"
            tvBpm.text = "- bpm"
        }
    }

    private fun ensureSensorsPermission(): Boolean {
        val perms = mutableListOf(Manifest.permission.BODY_SENSORS)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            perms += Manifest.permission.FOREGROUND_SERVICE
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            perms += Manifest.permission.POST_NOTIFICATIONS
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            // Android 10+ activity recognition permission
            perms += Manifest.permission.ACTIVITY_RECOGNITION
        }
        val notGranted = perms.filter { ContextCompat.checkSelfPermission(this, it) != PackageManager.PERMISSION_GRANTED }
        if (notGranted.isNotEmpty()) {
            ActivityCompat.requestPermissions(this, notGranted.toTypedArray(), 10)
            return false
        }
        return true
    }

    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == 10) {
            val allGranted = grantResults.all { it == PackageManager.PERMISSION_GRANTED }
            if (allGranted && pendingStartId > 0L) {
                val id = pendingStartId
                val dur = if (pendingDurationSec > 0L) pendingDurationSec else 20L * 60L
                pendingStartId = -1L
                pendingDurationSec = -1L
                startMeasurement(id, dur)
            } else if (!allGranted) {
                toast("권한 허용 후 다시 시도해 주세요.")
            }
        }
    }

    private fun deviceUuid(): String =
        Settings.Secure.getString(contentResolver, Settings.Secure.ANDROID_ID) ?: "unknown"

    private fun toast(msg: String) = Toast.makeText(this, msg, Toast.LENGTH_SHORT).show()

    private val prefs by lazy {
        getSharedPreferences("mindstage_watch", Context.MODE_PRIVATE)
    }

    companion object {
        private const val KEY_INTERVIEW_ID = "interview_id"
        private const val KEY_DEVICE_SECRET = "device_secret"
        private const val KEY_FCM_TOKEN = "fcm_token"
        private const val KEY_LAST_COMMIT_AT = "last_commit_at"
        // Service action constants
        const val ACTION_START = "com.example.mindstagewatch.START"
        const val ACTION_STOP_AND_POST = "com.example.mindstagewatch.STOP_AND_POST"
    }
}
