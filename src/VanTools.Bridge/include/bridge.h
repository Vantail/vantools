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

/**
 * Packs a directory into a .van archive using the underlying C# implementation.
 *
 * @param source A null-terminated UTF-8 string representing the source directory path.
 * @param target A null-terminated UTF-8 string representing the target directory path.
 *
 * @return 0 on success (ExitCodes.Success).
 * @return 1 on runtime error (ExitCodes.RuntimeError).
 * @return 2 on usage error (ExitCodes.UsageError).
 *
 * @note The caller retains ownership of the string buffers. The bridge does not
 *       modify or free them.
 * @note If either pointer is NULL, the function returns 1 immediately.
 */
EXPORT int van_pack(const char* source, const char* target);

#ifdef __cplusplus
}
#endif

#endif
