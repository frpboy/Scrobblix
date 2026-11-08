package dev.scrobblix.core

/** Local observation only, never proof of socket write or provider receipt. Unknown-only, no retry input. */
enum class UnknownEvidence {
    IO_AFTER_SEND_PERMIT, RESPONSE_JSON_INVALID, RESPONSE_BODY_LIMIT, RESPONSE_STREAM_ABSENT,
    BROKER_REPORTED_FORWARD_UNKNOWN, PROVIDER_ERROR_8, INTERRUPTED_SEND_RECOVERED;
    companion object {
        fun read(value:String?):UnknownEvidence?=entries.firstOrNull{it.name==value}
    }
}
/** Fixed typed evidence. Never persist or display a cause/message/body. */
class ResponseEvidenceIOException(val evidence:UnknownEvidence):java.io.IOException()
