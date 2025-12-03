package com.example.mindstagewatch.net

import com.example.mindstagewatch.BuildConfig
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import java.util.concurrent.TimeUnit
import okio.Buffer
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

// --- DTOs ---
data class RegisterRequest(
    val uuid: String,
    val modelName: String
)

data class GalaxyData(
    val galaxyWatchId: Long?,
    val modelName: String?
)

data class RegisterResponse(
    val success: Boolean,
    val message: String?,
    val code: Int?,
    val data: GalaxyData?
)

data class HeartbeatPoint(
    val bpm: Int,
    val measuredAt: String
)

data class HeartbeatBatchRequest(
    val interviewId: Long,
    val deviceUuid: String,
    val dataPoints: List<HeartbeatPoint>
)

data class ApiResult(
    val success: Boolean,
    val message: String?,
    val code: Int?,
    val data: Any?
)

data class TokenCommitRequest(
    val uuid: String,
    val token: String
)

data class TokenCommitResponse(
    val deviceSecret: String?
)

data class ApiResultTokenCommit(
    val success: Boolean,
    val message: String?,
    val code: Int?,
    val data: TokenCommitResponse?
)

object Api {
    private const val DEFAULT_BASE_URL = "https://mindstage.duckdns.org/"

    @Volatile private var baseUrl: String = DEFAULT_BASE_URL
    fun setBaseUrl(url: String) {
        baseUrl = if (url.endsWith("/")) url else "$url/"
    }

    @Volatile private var tokenProvider: () -> String? = { null }
    fun setTokenProvider(p: () -> String?) { tokenProvider = p }
    fun setStaticToken(token: String) { tokenProvider = { token } }

    @Volatile private var uuidProvider: () -> String? = { null }
    fun setUuidProvider(p: () -> String?) { uuidProvider = p }

    @Volatile private var deviceSecretProvider: () -> String? = { null }
    fun setDeviceSecretProvider(p: () -> String?) { deviceSecretProvider = p }

    private val authInterceptor = Interceptor { chain ->
        val rb = chain.request().newBuilder()
        val t = tokenProvider()
        if (!t.isNullOrBlank()) rb.addHeader("Authorization", "Bearer $t")
        chain.proceed(rb.build())
    }

    private val hmacInterceptor = Interceptor { chain ->
        val req = chain.request()
        val path = req.url.encodedPath
        val method = req.method.uppercase()
        val needsHmac = (method == "POST") && (path == "/api/GalaxyWatch/token/commit" || path == "/api/heartbeat/batch")
        if (!needsHmac) {
            return@Interceptor chain.proceed(req)
        }
        val uuid = uuidProvider() ?: return@Interceptor chain.proceed(req)
        val secret = deviceSecretProvider() ?: return@Interceptor chain.proceed(req)

        // Body bytes
        val body = req.body
        val buffer = Buffer()
        if (body != null) {
            body.writeTo(buffer)
        }
        val bodyBytes = buffer.readByteArray()
        val contentSha = sha256Hex(bodyBytes)

        val ts = System.currentTimeMillis().toString()
        val nonce = randomNonce()
        val pathWithQuery = if (req.url.query.isNullOrBlank()) path else "$path?${req.url.query}"
        val canonical = listOf(method, pathWithQuery, ts, nonce, contentSha).joinToString("\n")
        val signature = hmacBase64(secret, canonical)

        val newReq = req.newBuilder()
            .addHeader("X-Watch-UUID", uuid)
            .addHeader("X-Timestamp", ts)
            .addHeader("X-Nonce", nonce)
            .addHeader("X-Content-SHA256", contentSha)
            .addHeader("X-Signature", signature)
            .build()
        chain.proceed(newReq)
    }

    private val logging = HttpLoggingInterceptor().apply {
        level = if (BuildConfig.DEBUG)
            HttpLoggingInterceptor.Level.BODY
        else
            HttpLoggingInterceptor.Level.NONE
    }

    private val client: OkHttpClient by lazy {
        OkHttpClient.Builder()
            .addInterceptor(authInterceptor)
            .addInterceptor(hmacInterceptor)
            .addInterceptor(logging)
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .build()
    }

    // Moshi가 코틀린을 인식하도록 KotlinJsonAdapterFactory 추가
    private val moshi: Moshi by lazy {
        Moshi.Builder()
            .add(KotlinJsonAdapterFactory())
            .build()
    }

    private val retrofit: Retrofit by lazy {
        Retrofit.Builder()
            .baseUrl(baseUrl)
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(moshi)) // 수정된 Moshi 객체 사용
            .build()
    }

    val galaxy: GalaxyWatchApi by lazy { retrofit.create(GalaxyWatchApi::class.java) }

    private fun sha256Hex(bytes: ByteArray): String {
        val md = MessageDigest.getInstance("SHA-256")
        val dig = md.digest(bytes)
        val sb = StringBuilder(dig.size * 2)
        for (b in dig) sb.append(String.format("%02x", b))
        return sb.toString()
    }

    private fun hmacBase64(secret: String, canonical: String): String {
        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(secret.toByteArray(StandardCharsets.UTF_8), "HmacSHA256"))
        val sig = mac.doFinal(canonical.toByteArray(StandardCharsets.UTF_8))
        return java.util.Base64.getEncoder().encodeToString(sig)
    }

    private fun randomNonce(): String {
        val bytes = ByteArray(16)
        java.security.SecureRandom().nextBytes(bytes)
        return java.util.Base64.getEncoder().encodeToString(bytes)
    }
}
