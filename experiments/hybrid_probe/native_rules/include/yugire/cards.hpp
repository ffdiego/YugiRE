#pragma once
#include "card_request.hpp"

namespace yugire { namespace cards {
unsigned short sparks(const CardRequest* request, void* opaque);
unsigned short ookazi(const CardRequest* request, void* opaque);
}} // namespace yugire::cards
