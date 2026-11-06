# Artwork metadata guard (issue129)

Safety change, not aggregate cache eviction. Historical cache files are not deleted or replaced by this guard. Invalid cache content stays on disk but is not used. Limits:8MiB compressed,8192px on either axis,16million total pixels, one frame. These are conservative engineering limits, not an estimate of actual decoder working memory. Animated/multi-frame images and formats outside explicit PNG/JPEG/GIF/BMP/WebP built-in OS codecs fail closed. The tradeoff is some formerly visible artwork now shows the existing placeholder.

- Remote metadata downloads require the injected native validator before writing cache bytes. Missing validator fails closed. Cache hits are validated too.
- GSMTC thumbnails validate before write and on memory/disk cache hits.
- Desktop display independently reads a bounded snapshot of old or new files, validates its metadata and passes those exact in-memory bytes to BitmapImage.SetSourceAsync. It no longer validates one file then reopens a mutable URI. Per-image generations prevent a late earlier load from replacing a newer selection. Already displayed validated bytes do not reopen when the same path is polled.
- No GetPixelDataAsync or GetSoftwareBitmapAsync is called by the validator. Native codec initialization still parses untrusted input and is not a memory/CPU sandbox. Current Windows servicing and separate runtime/security qualification remain required.

## Proof and limits

Local cross-target Windows test project builds with0warnings/errors. Actual Windows execution is pending CI. Synthetic fixtures contain a valid1x1 PNG,100000x1 PNG (470bytes),5000x4000 PNG (85909bytes), two-frame GIF and malformed PNG. Oversized PNGs contain complete zlib-compressed scanlines, not just forged dimensions. Tests never request full pixel allocation for these fixtures.

Native Windows tests cover valid/rejected metadata, checked-width pixel math, compressed streaming bound, actual remote new/old cache paths, missing validator, actual GSMTC stream-reference new/old cache paths and byte-preservation. These tests do not render WinUI. Async old/new selection, fallback appearance, closed-window behavior and real Windows decoder/UI pixels still require direct visual/runtime qualification. No production-ready, exploit-proof or fixed maximum-memory claim is made.

Sources checked:
- https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.bitmapdecoder
- https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.bitmapdecoder.createasync
- https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.bitmapdecoder.pixelwidth
