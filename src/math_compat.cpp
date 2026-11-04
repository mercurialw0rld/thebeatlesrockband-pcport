// Clang 19 may lower SIMD round-to-nearest-even to this C23 entry point,
// which the Windows UCRT does not provide. Use IEEE-754 bits so the result
// does not depend on the host rounding mode, and preserve signed zero.
#include <bit>
#include <cstdint>

extern "C" float roundevenf(float value) {
  const std::uint32_t bits = std::bit_cast<std::uint32_t>(value);
  const std::uint32_t sign = bits & 0x80000000u;
  const std::uint32_t magnitude = bits & 0x7fffffffu;
  const int exponent = int(magnitude >> 23) - 127;
  if (exponent >= 23) return value;  // Already integral, infinity or NaN.
  if (exponent < -1) return std::bit_cast<float>(sign);
  if (exponent == -1) {
    return std::bit_cast<float>(sign | (magnitude > 0x3f000000u ? 0x3f800000u : 0u));
  }
  const std::uint32_t unit = 1u << (23 - exponent);
  const std::uint32_t mask = unit - 1u;
  const std::uint32_t fraction = magnitude & mask;
  const std::uint32_t halfway = unit >> 1;
  std::uint32_t rounded = magnitude & ~mask;
  if (fraction > halfway || (fraction == halfway && (rounded & unit))) rounded += unit;
  return std::bit_cast<float>(sign | rounded);
}
