#pragma once
#include <mutex>
std::unique_lock<std::recursive_mutex> LockInputApi();
