package com.example.mindstagewatch.fcm

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.provider.Settings
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import com.example.mindstagewatch.MainActivity
import com.example.mindstagewatch.hr.HrCaptureService
import com.example.mindstagewatch.net.Api
import com.example.mindstagewatch.net.TokenCommitRequest
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class MyFirebaseMessagingService : FirebaseMessagingService() {

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onNewToken(token: String) {
        Log.d(TAG, "FCM new token: $token")
        // 토큰 로컬 저장
        getPrefs().edit().putString(KEY_FCM_TOKEN, token).apply()

        // 서버 전송: UUID와 함께 커밋
        scope.launch {
            try {
                val req = TokenCommitRequest(uuid = deviceUuid(), token = token)
                // HMAC 부트스트랩: uuidProvider를 등록 (초기 호출에도 헤더에 uuid 전달 가능)
                Api.setUuidProvider { deviceUuid() }
                Api.setDeviceSecretProvider { getPrefs().getString(KEY_DEVICE_SECRET, null) }

                val res = Api.galaxy.commitFcmToken(req)
                if (res.success) {
                    val secret = res.data?.deviceSecret
                    if (!secret.isNullOrBlank()) {
                        getPrefs().edit().putString(KEY_DEVICE_SECRET, secret).apply()
                        // 이후 요청부터 인터셉터가 자동으로 HMAC 헤더 부착
                        Api.setDeviceSecretProvider { secret }
                        Log.d(TAG, "Device secret stored for HMAC")
                    }
                    Log.d(TAG, "FCM token committed to server")
                } else {
                    Log.w(TAG, "FCM token commit failed: ${res.message}")
                }
            } catch (e: Exception) {
                Log.e(TAG, "Failed to commit FCM token to server", e)
            }
        }
    }

    override fun onMessageReceived(remoteMessage: RemoteMessage) {
        Log.d(TAG, "FCM message received: data=${remoteMessage.data}")
        val action = remoteMessage.data["action"] ?: return

        when (action) {
            ACTION_REQUEST_HEALTH_DATA -> {
                val interviewId = remoteMessage.data["interviewId"]?.toLongOrNull()
                val durationSec = remoteMessage.data["durationSec"]?.toLongOrNull() ?: DEFAULT_DURATION_SEC

                if (interviewId == null) {
                    Log.w(TAG, "Missing or invalid interviewId in push payload")
                    return
                }

                Log.d(TAG, "Showing start notification for interviewId=$interviewId, durationSec=$durationSec")
                // 보수적으로 항상 사용자 액션을 요구 (Wear OS/Android 13+의 FGS 제한 회피)
                showStartNotification(interviewId, durationSec)
            }
            ACTION_STOP_HEALTH_DATA -> {
                val interviewId = remoteMessage.data["interviewId"]?.toLongOrNull()
                if (interviewId == null) {
                    Log.w(TAG, "Missing or invalid interviewId in stop payload")
                    return
                }
                Log.d(TAG, "Showing stop notification for interviewId=$interviewId")
                showStopNotification(interviewId)
            }
            else -> {
                Log.d(TAG, "Unknown action: $action")
            }
        }
    }

    private fun hasSensorsPermission(): Boolean {
        val body = ContextCompat.checkSelfPermission(this, android.Manifest.permission.BODY_SENSORS) == PackageManager.PERMISSION_GRANTED
        val activityOk = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            ContextCompat.checkSelfPermission(this, android.Manifest.permission.ACTIVITY_RECOGNITION) == PackageManager.PERMISSION_GRANTED
        } else true
        return body && activityOk
    }

    private fun showStartNotification(interviewId: Long, durationSec: Long) {
        // Android 13+ 알림 권한 체크
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, android.Manifest.permission.POST_NOTIFICATIONS) 
                != PackageManager.PERMISSION_GRANTED) {
                Log.e(TAG, "POST_NOTIFICATIONS permission not granted! Cannot show notification.")
                return
            }
        }

        val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        val channelId = "ms_watch_ctrl"
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val ch = NotificationChannel(channelId, "MindStage Control", NotificationManager.IMPORTANCE_HIGH)
            nm.createNotificationChannel(ch)
            Log.d(TAG, "Notification channel created: $channelId")
        }
        val intent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            putExtra(EXTRA_START_INTERVIEW_ID, interviewId)
            putExtra(EXTRA_START_DURATION_SEC, durationSec)
        }
        val piFlags = PendingIntent.FLAG_UPDATE_CURRENT or (if (Build.VERSION.SDK_INT >= 23) PendingIntent.FLAG_IMMUTABLE else 0)
        val pi = PendingIntent.getActivity(this, 1001, intent, piFlags)
        val notif = NotificationCompat.Builder(this, channelId)
            .setSmallIcon(com.example.mindstagewatch.R.drawable.ic_launcher_foreground)
            .setContentTitle("심박 측정 요청")
            .setContentText("면접 #$interviewId • ${durationSec / 60}분 측정 시작")
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(pi)
            .addAction(0, "시작", pi)
            .build()
        nm.notify(2001, notif)
        Log.d(TAG, "Start notification posted: interviewId=$interviewId")
    }

    private fun showStopNotification(interviewId: Long) {
        // Android 13+ 알림 권한 체크
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, android.Manifest.permission.POST_NOTIFICATIONS) 
                != PackageManager.PERMISSION_GRANTED) {
                Log.e(TAG, "POST_NOTIFICATIONS permission not granted! Cannot show notification.")
                return
            }
        }

        val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        val channelId = "ms_watch_ctrl"
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val ch = NotificationChannel(channelId, "MindStage Control", NotificationManager.IMPORTANCE_HIGH)
            nm.createNotificationChannel(ch)
            Log.d(TAG, "Notification channel created: $channelId")
        }
        val intent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            putExtra(EXTRA_STOP_REQUEST, true)
            putExtra(EXTRA_START_INTERVIEW_ID, interviewId)
        }
        val piFlags = PendingIntent.FLAG_UPDATE_CURRENT or (if (Build.VERSION.SDK_INT >= 23) PendingIntent.FLAG_IMMUTABLE else 0)
        val pi = PendingIntent.getActivity(this, 2002, intent, piFlags)
        val notif = NotificationCompat.Builder(this, channelId)
            .setSmallIcon(com.example.mindstagewatch.R.drawable.ic_launcher_foreground)
            .setContentTitle("심박 측정 종료")
            .setContentText("면접 #$interviewId 측정 종료")
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(pi)
            .addAction(0, "종료", pi)
            .build()
        nm.notify(2002, notif)
        Log.d(TAG, "Stop notification posted: interviewId=$interviewId")
    }

    private fun getPrefs() =
        getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

    private fun deviceUuid(): String =
        Settings.Secure.getString(contentResolver, Settings.Secure.ANDROID_ID) ?: "unknown"

    companion object {
        private const val TAG = "MyFcmService"
        private const val PREFS_NAME = "mindstage_watch"
        private const val KEY_FCM_TOKEN = "fcm_token"
        private const val KEY_DEVICE_SECRET = "device_secret"

        const val ACTION_REQUEST_HEALTH_DATA = "request_health_data"
        const val ACTION_STOP_HEALTH_DATA = "stop_health_data"
        const val EXTRA_START_INTERVIEW_ID = "extra_start_interview_id"
        const val EXTRA_START_DURATION_SEC = "extra_start_duration_sec"
        const val EXTRA_STOP_REQUEST = "extra_stop_request"
        private const val DEFAULT_DURATION_SEC = 20 * 60L // 20분
    }
}



