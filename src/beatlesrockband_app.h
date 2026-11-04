// beatlesrockband - ReXGlue Recompiled Project
//
// Customize your app by overriding virtual hooks from rex::ReXApp.

#pragma once

#include "windows_audio.h"
#include <filesystem>
#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/rex_app.h>
#include <rex/ui/overlay/achievement_notification.h>
REXCVAR_DECLARE(std::string, game_data_root);
REXCVAR_DECLARE(std::string, user_data_root);
REXCVAR_DECLARE(std::string, cache_root);
REXCVAR_DECLARE(std::string, update_data_root);
REXCVAR_DECLARE(std::string, metadata_root);
REXCVAR_DECLARE(bool, achievement_popups);

class BeatlesRockbandApp : public rex::ReXApp {
public:
  using rex::ReXApp::ReXApp;

  std::unique_ptr<rex::ui::AchievementNotificationDialog> CreateAchievementNotificationDialog() override {
    // Keep achievement tracking/F7; the default always-attached empty toast
    // otherwise forces continuous UI redraws throughout rhythm gameplay.
    if(!REXCVAR_GET(achievement_popups))return nullptr;
    return rex::ReXApp::CreateAchievementNotificationDialog();
  }

  static std::unique_ptr<rex::ui::WindowedApp>
  Create(rex::ui::WindowedAppContext &ctx) {
    return std::unique_ptr<BeatlesRockbandApp>(
        new BeatlesRockbandApp(ctx, "beatlesrockband", PPCImageConfig));
  }

  void OnPreSetup(rex::RuntimeConfig &config) override {
    ConfigureWindowsAudio(config);
  }
  void OnConfigurePaths(rex::PathConfig &paths) override {
    // Command-line strings are UTF-8, not the Windows ANSI code page.
    // Resolve relative data paths before making the executable folder current.
    auto set = [](const std::string &value, std::filesystem::path &target) {
      if (!value.empty())
        target = std::filesystem::absolute(rex::to_path(value));
    };
    set(REXCVAR_GET(game_data_root), paths.game_data_root);
    set(REXCVAR_GET(user_data_root), paths.user_data_root);
    set(REXCVAR_GET(cache_root), paths.cache_root);
    set(REXCVAR_GET(update_data_root), paths.update_data_root);
    set(REXCVAR_GET(metadata_root), paths.metadata_root);
    // The prebuilt SDK feeds path.string() to TOML's UTF-8 parser. An ASCII
    // filename avoids the ANSI/UTF-8 round trip for folders such as "portátil".
    // Keep this working directory for subsequent config saves as well.
    std::filesystem::current_path(rex::filesystem::GetExecutableFolder());
    paths.config_path = "beatlesrockband.toml";
  }
  void OnPostSetup() override {
    REXLOG_INFO("Scene stability: nonblocking_pipelines={}, achievement_popups={}, present_from_gpu_thread={}",
                rex::cvar::GetFlagByName("d3d12_nonblocking_pipeline_creation"),
                rex::cvar::GetFlagByName("achievement_popups"),
                rex::cvar::GetFlagByName("host_present_from_non_ui_thread"));
    REXLOG_INFO("Graphics configuration: waitable={}, precise_timing={}, clear_redirect={}, primary_submit={}, gpu_trace={}",
                rex::cvar::GetFlagByName("d3d12_present_waitable"),
                rex::cvar::GetFlagByName("gpu_precise_timing"),
                rex::cvar::GetFlagByName("clear_msaa_redirect"),
                rex::cvar::GetFlagByName("d3d12_submit_on_primary_buffer_end"),
                rex::cvar::GetFlagByName("trace_gpu_timing"));
    REXLOG_INFO("Keyboard configuration: mnk_mode={}, guitar={}, green={}, "
                "red={}, yellow={}, blue={}, orange={}, strum={}/{}",
                rex::cvar::GetFlagByName("mnk_mode"),
                rex::cvar::GetFlagByName("keyboard_guitar"),
                rex::cvar::GetFlagByName("keybind_a"),
                rex::cvar::GetFlagByName("keybind_b"),
                rex::cvar::GetFlagByName("keybind_y"),
                rex::cvar::GetFlagByName("keybind_x"),
                rex::cvar::GetFlagByName("keybind_left_shoulder"),
                rex::cvar::GetFlagByName("keybind_dpad_up"),
                rex::cvar::GetFlagByName("keybind_dpad_down"));
  }
  // Override virtual hooks for customization:
  // void OnPostInitLogging() override {}
  // void OnPreSetup(rex::RuntimeConfig& config) override {}
  // void OnLoadXexImage(std::string& xex_image) override {}
  // void OnPostLoadXexImage() override {}
  // void OnPostSetup() override {}
  // void OnCreateDialogs(rex::ui::ImGuiDrawer* drawer) override {}
  // std::unique_ptr<rex::ui::ImGuiDialog> CreateAchievementsOverlay() override;
  // std::unique_ptr<rex::ui::AchievementNotificationDialog>
  // CreateAchievementNotificationDialog() override;
  // void OnShutdown() override {}
  // void OnConfigurePaths(rex::PathConfig& paths) override {}
};
