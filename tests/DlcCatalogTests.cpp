#include "../src/dlc_song_catalog.h"
#include <cstdio>
#include <stdexcept>
#include <string>
using beatles::dlc::CustomSongStems;
static int checks;
static void Check(bool ok,const char* label){if(!ok)throw std::runtime_error(label);++checks;std::printf("PASS %s\n",label);}
static void Reject(const std::string& text,const char* label){bool rejected=false;try{CustomSongStems(text);}catch(const std::runtime_error&){rejected=true;}Check(rejected,label);}
int main(){try{
 auto good=std::string("(example (name \"Example (a; b)\") (game_origin beatles) (song (name songs/example/example)))");
 Check(CustomSongStems(good)==std::vector<std::string>{"songs/example/example"},"recognizes a TBRB custom catalog");
 Check(CustomSongStems("; comment\n"+good+"\n; comment\n"+good).size()==2,"validates every song in a pack");
 Check(CustomSongStems("\xEF\xBB\xBF"+good).size()==1,"accepts UTF-8 BOM");
 Reject("","rejects empty catalog");Reject("; (fake (game_origin beatles))","comments do not identify a package");
 Reject("(x (name \"(game_origin beatles)\") (game_origin rb3) (song (name songs/x/x)))","strings cannot spoof TBRB origin");
 Reject("(x ; (game_origin beatles)\n(song (name songs/x/x)))","comment cannot spoof TBRB origin");
 Reject("(x (nested (game_origin beatles)) (song (name songs/x/x)))","nested unrelated origin is rejected");
 Reject("(x (game_origin rb3) (song (name songs/x/x)))","other game origin rejected");
 Reject(good+" (other (game_origin rb3) (song (name songs/other/other)))","mixed game pack rejected");
 Reject("(x (game_origin beatles) (game_origin rb3) (song (name songs/x/x)))","duplicate origin rejected");
 Reject("(x (game_origin beatles) (song (name songs/x/x) (name songs/y/y)))","ambiguous asset name rejected");
 Reject("(x (game_origin beatles))","missing assets rejected");
 Reject("(x (game_origin beatles) (song (name songs/../x)))","path traversal rejected");
 Reject("(x (game_origin beatles) (song (name C:/x/x)))","absolute asset path rejected");
 Reject("(x (game_origin beatles) (song (name songs//x)))","empty component rejected");
 Reject("(x (game_origin beatles) (song (name songs/x/x))) )","stray closing parenthesis rejected");
 Reject("(x (game_origin beatles) (song (name \"unfinished)))","unterminated string rejected");
 Reject(std::string(70,'(')+"x"+std::string(70,')'),"bounded parser depth");
 Reject(std::string(1024*1024+1,' '),"bounded catalog size");
 std::printf("%d catalog checks passed\n",checks);return 0;
}catch(const std::exception& error){std::printf("FAIL %s\n",error.what());return 1;}}
