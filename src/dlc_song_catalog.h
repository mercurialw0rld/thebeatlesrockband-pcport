#pragma once
#include <cctype>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace beatles::dlc {
// This parser is only for verifying plaintext song catalogs in generic-title
// custom packages. It does not reinterpret official DLC or convert RB3 songs.
struct CatalogNode {
 bool list=false;
 std::string atom;
 std::vector<CatalogNode> children;
};
class CatalogParser {
 std::string_view text_;
 size_t pos_=0,nodes_=0;
 void Space(){
  while(pos_<text_.size()){
   if(std::isspace(static_cast<unsigned char>(text_[pos_]))){++pos_;continue;}
   if(text_[pos_]==';'){while(pos_<text_.size()&&text_[pos_]!='\n')++pos_;continue;}
   break;
  }
 }
 CatalogNode Read(unsigned depth){
  if(depth>64||++nodes_>100000)throw std::runtime_error("The custom song catalog is too complex.");
  Space();if(pos_==text_.size()||text_[pos_]==')')throw std::runtime_error("The custom song catalog is incomplete.");
  CatalogNode node;
  if(text_[pos_]=='('){
   node.list=true;++pos_;
   for(;;){Space();if(pos_==text_.size())throw std::runtime_error("The custom song catalog has unclosed parentheses.");if(text_[pos_]==')'){++pos_;break;}node.children.push_back(Read(depth+1));}
  }else if(text_[pos_]=='"'){
   ++pos_;bool closed=false;
   while(pos_<text_.size()){
    char ch=text_[pos_++];if(ch=='"'){closed=true;break;}
    if(ch=='\\'){if(pos_==text_.size())break;ch=text_[pos_++];}
    if(!ch)throw std::runtime_error("The custom song catalog contains binary data.");node.atom+=ch;
   }
   if(!closed)throw std::runtime_error("The custom song catalog has an unclosed string.");
  }else{
   while(pos_<text_.size()){
    char ch=text_[pos_];if(std::isspace(static_cast<unsigned char>(ch))||ch=='('||ch==')'||ch==';')break;
    if(!ch||ch=='"')throw std::runtime_error("The custom song catalog contains invalid tokens.");node.atom+=ch;++pos_;
   }
  }
  return node;
 }
 public:
 explicit CatalogParser(std::string_view text):text_(text){
  if(text.size()>1024*1024)throw std::runtime_error("The custom song catalog is too large.");
  if(text_.starts_with("\xEF\xBB\xBF"))pos_=3;
 }
 std::vector<CatalogNode> Parse(){std::vector<CatalogNode> nodes;for(;;){Space();if(pos_==text_.size())break;nodes.push_back(Read(0));}return nodes;}
};
inline const CatalogNode* Property(const CatalogNode& node,std::string_view name){
 const CatalogNode* found=nullptr;
 for(const auto& child:node.children){
  if(child.list&&!child.children.empty()&&!child.children[0].list&&child.children[0].atom==name){
   if(found)throw std::runtime_error("The custom song catalog contains duplicate properties.");found=&child;
  }
 }
 return found;
}
inline std::string Value(const CatalogNode* property){
 if(!property||property->children.size()!=2||property->children[1].list)throw std::runtime_error("The custom song catalog is missing required TBRB metadata.");
 return property->children[1].atom;
}
inline std::vector<std::string> CustomSongStems(std::string_view text){
 const auto songs=CatalogParser(text).Parse();
 if(songs.empty())throw std::runtime_error("The custom package has no songs.");
 std::vector<std::string> result;
 for(const auto& song:songs){
  if(!song.list||song.children.empty()||song.children[0].list||song.children[0].atom.empty()||Value(Property(song,"game_origin"))!="beatles")throw std::runtime_error("The generic-title package does not declare TBRB songs (game_origin beatles).");
  const auto* audio=Property(song,"song");
  if(!audio)throw std::runtime_error("The custom song is missing its asset definition.");
  auto stem=Value(Property(*audio,"name"));
  if(!stem.starts_with("songs/")||stem.size()>220||stem.find('\\')!=std::string::npos||stem.find(':')!=std::string::npos)throw std::runtime_error("The custom song has an invalid asset path.");
  size_t start=0,parts=0;
  for(size_t end=0;end<=stem.size();++end)if(end==stem.size()||stem[end]=='/'){
   auto part=stem.substr(start,end-start);if(part.empty()||part=="."||part=="..")throw std::runtime_error("The custom song has an unsafe asset path.");start=end+1;++parts;
  }
  if(parts!=3)throw std::runtime_error("The custom song must use songs/folder/name assets.");
  result.push_back(std::move(stem));
 }
 return result;
}
}
