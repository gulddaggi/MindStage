package com.example.mindstagewatch.net
import retrofit2.http.Body
import retrofit2.http.POST

interface GalaxyWatchApi {
    @POST("api/GalaxyWatch/register")
    suspend fun register(@Body req: RegisterRequest): RegisterResponse

    @POST("api/heartbeat/batch")
    suspend fun postHeartbeatBatch(@Body req: HeartbeatBatchRequest): ApiResult

    @POST("api/GalaxyWatch/token/commit")
    suspend fun commitFcmToken(@Body req: TokenCommitRequest): ApiResultTokenCommit
}