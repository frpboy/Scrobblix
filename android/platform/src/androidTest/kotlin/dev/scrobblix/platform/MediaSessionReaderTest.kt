package dev.scrobblix.platform

import android.content.ComponentName
import android.media.MediaMetadata
import android.media.session.MediaSession
import android.media.session.PlaybackState
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

/** Synthetic native sessions only, not third-party player/background qualification. */
@RunWith(AndroidJUnit4::class)
class MediaSessionReaderTest {
    @Test fun nativeSessionEqualityTagsAreEphemeralAndNeverUseSerializedTokens() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val reader=MediaSessionReader(context,ComponentName(context,ScrobblixMediaListener::class.java))
        val first=MediaSession(context,"synthetic-first");val second=MediaSession(context,"synthetic-second")
        try {
            val tag=reader.identityFor(first.sessionToken)
            assertEquals(tag,reader.identityFor(first.controller.sessionToken))
            assertNotEquals(tag,reader.identityFor(second.sessionToken))
            val fresh=MediaSessionReader(context,ComponentName(context,ScrobblixMediaListener::class.java))
            assertNotEquals(tag,fresh.identityFor(first.sessionToken))
        } finally {first.release();second.release()}
    }
    @Test fun accessIsRequiredWithoutEnablingNotificationListener() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val reader=MediaSessionReader(context,ComponentName(context,ScrobblixMediaListener::class.java))
        assertEquals(MediaReadResult.AccessRequired,reader.read())
    }
    @Test fun nativeSessionMetadataIsReadAndUnsafeFieldsAreRejected() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val session=MediaSession(context,"synthetic-scrobblix-test")
        try {
            session.setMetadata(MediaMetadata.Builder().putString(MediaMetadata.METADATA_KEY_ARTIST,"synthetic artist")
                .putString(MediaMetadata.METADATA_KEY_TITLE,"synthetic title")
                .putString(MediaMetadata.METADATA_KEY_MEDIA_ID,"synthetic-id")
                .putLong(MediaMetadata.METADATA_KEY_DURATION,300000).build())
            // Static synthetic position: native controller may extrapolate at nonzero playback speed.
            session.setPlaybackState(PlaybackState.Builder().setState(PlaybackState.STATE_PLAYING,12000,0f).build())
            val result=MediaSessionReader.snapshot(session.controller)
            assertEquals("synthetic title",result.title);assertEquals("synthetic artist",result.artist)
            assertEquals("synthetic-id",result.mediaId)
            assertEquals(300000L,result.durationMs);assertEquals(12000L,result.positionMs)
            assertEquals(PlaybackState.STATE_PLAYING,result.state)
            session.setMetadata(MediaMetadata.Builder().putString(MediaMetadata.METADATA_KEY_ARTIST,"a".repeat(1025))
                .putString(MediaMetadata.METADATA_KEY_TITLE,"bad\nvalue")
                .putString(MediaMetadata.METADATA_KEY_ALBUM," ")
                .putString(MediaMetadata.METADATA_KEY_MEDIA_ID,"bad\nvalue")
                .putLong(MediaMetadata.METADATA_KEY_DURATION,-1).build())
            session.setPlaybackState(PlaybackState.Builder().setState(PlaybackState.STATE_PAUSED,-1,0f).build())
            val invalid=MediaSessionReader.snapshot(session.controller)
            assertNull(invalid.mediaId);assertNull(invalid.artist);assertNull(invalid.title);assertNull(invalid.album)
            assertNull(invalid.durationMs);assertNull(invalid.positionMs)
            assertEquals(PlaybackState.STATE_PAUSED,invalid.state)
        } finally { session.release() }
    }
}
