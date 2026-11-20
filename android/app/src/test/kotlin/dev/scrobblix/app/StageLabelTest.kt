package dev.scrobblix.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class StageLabelTest {
    @Test fun debugAlwaysDevelopmentPreview() {
        assertEquals("Development preview · not a release", StageLabel.forBuild(true, "0.1.0-alpha.3"))
        assertEquals("Development preview · not a release", StageLabel.forBuild(true, "1.0.0"))
    }
    @Test fun alphaReleaseShowsAlphaStage() {
        assertEquals("Alpha testing build · not a stable release", StageLabel.forBuild(false, "0.1.0-alpha.3"))
    }
    @Test fun betaReleaseShowsBetaStage() {
        assertEquals("Beta testing build · not a stable release", StageLabel.forBuild(false, "0.2.0-beta.1"))
    }
    @Test fun previewMarkersShowPreviewStage() {
        assertEquals("Preview build · not a stable release", StageLabel.forBuild(false, "1.0.0-rc.1"))
        assertEquals("Preview build · not a stable release", StageLabel.forBuild(false, "1.0.0-preview"))
    }
    @Test fun stableReleaseShowsNoBanner() {
        assertNull(StageLabel.forBuild(false, "1.0.0"))
        assertNull(StageLabel.forBuild(false, "2.3.1"))
    }
}
