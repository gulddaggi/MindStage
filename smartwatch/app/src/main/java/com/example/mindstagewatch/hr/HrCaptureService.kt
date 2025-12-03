package com.example.mindstagewatch.hr

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.Binder
import android.os.Build
import android.os.IBinder
import android.util.Log
import android.content.pm.ServiceInfo
import androidx.core.app.NotificationCompat
import com.example.mindstagewatch.R
import com.example.mindstagewatch.net.Api
import com.example.mindstagewatch.net.HeartbeatBatchRequest
import com.example.mindstagewatch.net.HeartbeatPoint
import kotlinx.coroutines.*
import java.text.SimpleDateFormat
import java.util.*

class HrCaptureService : Service(), SensorEventListener {

    // 1. 액티비티와 통신하기 위한 Binder와 Callback 인터페이스 정의
    inner class LocalBinder : Binder() {
        fun getService(): HrCaptureService = this@HrCaptureService
    }
    private val binder = LocalBinder()

    interface ServiceCallback {
        fun onBpmChanged(bpm: Int)
        fun onStatusChanged(msg: String)
        fun onPostDone(ok: Boolean, msg: String)
    }
    var callback: ServiceCallback? = null

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private lateinit var sm: SensorManager
    private var hrSensor: Sensor? = null

    private var measuring = false
    private var interviewId: Long = -1L
    private var deviceUuid: String = "unknown"

    private val buffer = Collections.synchronizedList(mutableListOf<HeartbeatPoint>())

    override fun onCreate() {
        super.onCreate()
        sm = getSystemService(Context.SENSOR_SERVICE) as SensorManager
        hrSensor = sm.getDefaultSensor(Sensor.TYPE_HEART_RATE)
        createChannel()
    }

    // 2. onBind에서 binder 객체를 반환하도록 구현
    override fun onBind(intent: Intent): IBinder = binder

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START -> {
                interviewId = intent.getLongExtra("interviewId", -1L)
                deviceUuid = intent.getStringExtra("deviceUuid") ?: "unknown"
                buffer.clear()
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                    startForeground(
                        NOTI_ID,
                        noti("심박수 측정 중…"),
                        ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH
                    )
                } else {
                    startForeground(NOTI_ID, noti("심박수 측정 중…"))
                }
                startMeasure()
                // 3. 콜백을 통해 상태 전달
                callback?.onStatusChanged("[측정 시작] interviewId=$interviewId")
            }
            ACTION_STOP_AND_POST -> {
                stopMeasure()
                callback?.onStatusChanged("[측정 종료] 전송 준비…")
                scope.launch { postBatchAndStopSelf() }
            }
        }
        return START_NOT_STICKY
    }

    override fun onDestroy() {
        stopMeasure()
        scope.cancel()
        super.onDestroy()
    }

    private fun startMeasure() {
        if (measuring) return
        if (hrSensor == null) {
            callback?.onStatusChanged("[오류] 심박수 센서 없음")
            stopSelf()
            return
        }
        measuring = true
        sm.registerListener(this, hrSensor, SensorManager.SENSOR_DELAY_NORMAL)
    }

    private fun stopMeasure() {
        if (!measuring) return
        measuring = false
        sm.unregisterListener(this)
    }

    override fun onSensorChanged(event: SensorEvent?) {
        if (event?.sensor?.type != Sensor.TYPE_HEART_RATE) return
        val bpm = event.values.firstOrNull()?.toInt() ?: return
        if (bpm <= 0) return

        val ts = fmt(System.currentTimeMillis())
        buffer += HeartbeatPoint(bpm = bpm, measuredAt = ts)

        // 4. 콜백을 통해 bpm 전달
        callback?.onBpmChanged(bpm)
        Log.d(TAG, "HR sample: $bpm bpm (buf=${buffer.size})")
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) = Unit

    private suspend fun postBatchAndStopSelf() {
        val points = synchronized(buffer) { buffer.toList().also { buffer.clear() } }
        if (points.isEmpty()) {
            callback?.onPostDone(false, "수집된 데이터가 없습니다.")
            stopSelf()
            return
        }

        try {
            val req = HeartbeatBatchRequest(interviewId = interviewId, deviceUuid = deviceUuid, dataPoints = points)
            val res = Api.galaxy.postHeartbeatBatch(req)
            callback?.onPostDone(res.success, res.message ?: "성공 (${points.size}건)")
        } catch (e: Exception) {
            Log.e(TAG, "Batch post failed", e)
            callback?.onPostDone(false, "전송 실패: ${e.message}")
        } finally {
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopSelf()
        }
    }

    private fun fmt(millis: Long): String {
        return SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US).format(Date(millis))
    }

    private fun createChannel() {
        if (Build.VERSION.SDK_INT >= 26) {
            val nm = getSystemService(NotificationManager::class.java)
            nm.createNotificationChannel(NotificationChannel(CH_ID, "HR Capture", NotificationManager.IMPORTANCE_LOW))
        }
    }

    private fun noti(text: String): Notification =
        NotificationCompat.Builder(this, CH_ID)
            .setSmallIcon(R.drawable.ic_launcher_foreground)
            .setContentTitle("MindStage Watch")
            .setContentText(text)
            .setOngoing(true)
            .build()

    companion object {
        private const val TAG = "HR"
        private const val CH_ID = "hr_capture"
        private const val NOTI_ID = 1001
        const val ACTION_START = "com.example.mindstagewatch.START"
        const val ACTION_STOP_AND_POST = "com.example.mindstagewatch.STOP_AND_POST"
    }
}
