#include "bridge.h"
#include <stddef.h>

// Forward declaration of the C# Native AOT export
extern int van_pack_cs(const char* source, const char* target);

EXPORT int van_pack(const char* source, const char* target) {
    if (source == NULL || target == NULL) {
        return 1; // Guard against null pointers before crossing the interop boundary
    }

    return van_pack_cs(source, target);
}
