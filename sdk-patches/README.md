# ReXGlue graphics patches

Base commit: `bd833a2ab69d305f99ddf57418fc38ffec8af93d`. Apply in order with `Build-Sdk.ps1`; `-VerifyOnly` checks the revision and both patches.

## 0001: graphics pacing

- `d3d12_present_waitable`: a waitable swapchain, maximum frame latency 2, waiting outside the shared queue lock. Falls back if unsupported and releases its handle at shutdown.
- `gpu_precise_timing`: sub-millisecond GPU waits without truncation to Sleep(0), preserving fence/interrupt checks and shutdown handling.
- `clear_msaa_redirect`: redirects recognized XDK clear passes and restores modified registers. Does not globally disable MSAA. Adapted from the FIFA reference patches.
- `trace_gpu_timing`: optional memory-only host-call timing, disabled in normal use. Does not measure GPU execution or monitor scanout.
- `REXGLUE_REGISTER_PACKAGE`: lets the private SDK opt out of the global CMake package registry. The script sets it OFF.

## 0002: nonblocking scene submission

With asynchronous shaders and `d3d12_nonblocking_pipeline_creation`, EndSubmission does not steal compiler jobs or join all workers. Pending draws retain existing async readiness behavior. Readiness and flush publication are atomic. Cache unload and shutdown still join pending workers.

The SDK default is false. The launcher defaults Performance mode to true and explicitly passes either value on next launch. Newly encountered effects may appear after compilation. The reported dreamscape transition and musical replay remain pending manual confirmation.

UI-thread presentation and disabled achievement popups are application settings, not part of patch 0002. Achievement tracking and F7 remain available.

## Checks and references

`tests/Test-SceneSubmission.ps1` tests the actual submission/join bodies with event/worker doubles. It covers async operation, persistence, legacy/synchronous waits, no-worker handling and shutdown joins. To reproduce the old blocking path, use `-Original -SdkSourceDirectory <clean-base-checkout>`.

These tests do not validate rendering. Compare cold/warm caches, complex scenes, audio continuity, chart timing and shutdown. Do not attribute an older version's performance measurements to a new build.

References: [ReXGlue](https://github.com/rexglue/rexglue-sdk), [FIFA graphics patches](https://github.com/HossEz/fifa-world-cup-2014-recompiled/tree/v0.1.6/patches/rexglue-sdk). Existing notices are included in this source bundle.
