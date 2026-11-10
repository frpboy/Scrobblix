package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
class MediaCandidateTest {
    private fun candidate(name:String,artist:String?="Artist",title:String?="Track",playing:Boolean=true)=MediaCandidate(name,artist,title,playing)
    @Test fun incompletePlayingSessionCannotStarveValidMusic() {
        assertEquals("music",selectMediaCandidate(listOf(candidate("call",null,null),candidate("music"))))
        assertEquals("music",selectMediaCandidate(listOf(candidate("browser",title=" "),candidate("music"))))
    }
    @Test fun validPlayingPreferredAndReaderOrderPreserved() {
        assertEquals("playing",selectMediaCandidate(listOf(candidate("paused",playing=false),candidate("playing"),candidate("other"))))
        assertEquals("first",selectMediaCandidate(listOf(candidate("first"),candidate("second"))))
    }
    @Test fun incompleteSamplesStillReachCollectorContinuityBoundary() {
        assertEquals("incomplete",selectMediaCandidate(listOf(candidate("incomplete",null,null),candidate("paused",playing=false))))
        assertEquals("paused",selectMediaCandidate(listOf(candidate("invalid",null,null,false),candidate("paused",playing=false))))
        assertEquals("incomplete",selectMediaCandidate(listOf(candidate("incomplete",null,null))))
        assertNull(selectMediaCandidate(emptyList<MediaCandidate<String>>()))
    }
}
