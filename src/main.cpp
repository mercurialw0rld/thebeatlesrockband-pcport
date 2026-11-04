// beatlesrockband - ReXGlue Recompiled Project

#include "generated/default/beatlesrockband_init.h"

#include "beatlesrockband_app.h"
REXCVAR_DEFINE_BOOL(achievement_popups, false, "UI", "Show optional achievement popups (continuous overlay rendering)")
    .lifecycle(rex::cvar::Lifecycle::kInitOnly);

REX_DEFINE_APP(beatlesrockband, BeatlesRockbandApp::Create)
