package com.example.mindstagewatch.boot

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import com.example.mindstagewatch.work.TokenSyncWorker
import java.util.concurrent.TimeUnit

class BootReceiver : BroadcastReceiver() {
	override fun onReceive(context: Context, intent: Intent?) {
		val constraints = Constraints.Builder()
			.setRequiredNetworkType(NetworkType.CONNECTED)
			.build()

		// 부팅/업데이트 직후 1회 동기화
		val oneTime = OneTimeWorkRequestBuilder<TokenSyncWorker>()
			.setConstraints(constraints)
			.build()
		WorkManager.getInstance(context).enqueueUniqueWork(
			"token-sync-on-boot",
			ExistingWorkPolicy.REPLACE,
			oneTime
		)

		// 주기 동기화(6h)
		val periodic = PeriodicWorkRequestBuilder<TokenSyncWorker>(6, TimeUnit.HOURS)
			.setConstraints(constraints)
			.build()
		WorkManager.getInstance(context).enqueueUniquePeriodicWork(
			"token-sync-periodic",
			ExistingPeriodicWorkPolicy.KEEP,
			periodic
		)
	}
}


