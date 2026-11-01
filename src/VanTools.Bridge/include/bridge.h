#ifndef VANTOOLS_BRIDGE_H
#define VANTOOLS_BRIDGE_H

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

EXPORT int van_pack(const char* source, const char* target);

#ifdef __cplusplus
}
#endif

#endif
