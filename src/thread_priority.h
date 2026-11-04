#pragma once
#include <windows.h>
namespace beatles {
// These threads sleep between short audio jobs. Give them precedence over
// background guest network polling on hosts with fewer cores than Xenon.
inline void PrioritizeAudioThread(){
 thread_local const bool applied=SetThreadPriority(GetCurrentThread(),THREAD_PRIORITY_ABOVE_NORMAL)!=0;
 (void)applied;
}
inline void DeprioritizeOfflineNetworkThread(){
 thread_local const bool applied=SetThreadPriority(GetCurrentThread(),THREAD_PRIORITY_BELOW_NORMAL)!=0;
 (void)applied;
}
}
