package com.example.mindstagewatch.work

import android.content.Context
import android.provider.Settings
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.example.mindstagewatch.net.Api
import com.example.mindstagewatch.net.TokenCommitRequest
import com.google.android.gms.tasks.Tasks
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

class TokenSyncWorker(appContext: Context, params: WorkerParameters) : CoroutineWorker(appContext, params) {

	override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
		val prefs = applicationContext.getSharedPreferences("mindstage_watch", Context.MODE_PRIVATE)
		val savedToken = prefs.getString(KEY_FCM_TOKEN, null)

		// 서버 베이스 URL은 기본값(프로덕션)을 사용
		Api.setUuidProvider { deviceUuid() }
		Api.setDeviceSecretProvider { prefs.getString(KEY_DEVICE_SECRET, null) }

		return@withContext try {
			val task = FirebaseMessaging.getInstance().token
			val token = Tasks.await(task)

			if (token.isNullOrBlank()) return@withContext Result.success()

			// 무조건 커밋하여 서버 쪽 토큰 유실을 보정 (idempotent)
			val res = Api.galaxy.commitFcmToken(TokenCommitRequest(uuid = deviceUuid(), token = token))
			if (res.success) {
				prefs.edit().putString(KEY_FCM_TOKEN, token).apply()
				prefs.edit().putLong(KEY_LAST_COMMIT_AT, System.currentTimeMillis()).apply()
				res.data?.deviceSecret?.let { secret ->
					if (!secret.isNullOrBlank()) {
						prefs.edit().putString(KEY_DEVICE_SECRET, secret).apply()
						Api.setDeviceSecretProvider { secret }
					}
				}
			}

			Result.success()
		} catch (_: Exception) {
			// 네트워크/일시 오류는 재시도
			Result.retry()
		}
	}

	private fun deviceUuid(): String =
		Settings.Secure.getString(applicationContext.contentResolver, Settings.Secure.ANDROID_ID) ?: "unknown"

	companion object {
		private const val KEY_FCM_TOKEN = "fcm_token"
		private const val KEY_DEVICE_SECRET = "device_secret"
		private const val KEY_LAST_COMMIT_AT = "last_commit_at"
	}
}


