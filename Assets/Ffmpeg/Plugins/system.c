#include <stdlib.h>
#include <stdio.h>
#include <signal.h>

#ifdef __cplusplus
extern  "C" {
#endif

void unity_ignoreSignals()
{
    //signal(SIGINT, SIG_IGN);
    //signal(SIGTERM, SIG_IGN);
    //signal(SIGQUIT, SIG_IGN);
    signal(SIGPIPE, SIG_IGN);
    //signal(SIGXCPU, SIG_IGN);
}

int unity_system(const char *command)
{
    return system(command);
}

void* unity_popen(const char *command, const char *type)
{
    return (void*)popen(command, type);
}

int unity_pclose(void* stream)
{
    return pclose((FILE*)stream);
}

int unity_fread(void* buf, int size, int n, void* fp)
{
    return fread(buf, (size_t)size, (size_t)n, (FILE*)fp);
}

void* unity_fgets(void* s, int n, void* stream)
{
    return fgets((char*)s, n, (FILE*)stream);
}

int unity_fputc(int c, void* fp)
{
    return fputc(c, (FILE*)fp);
}

#ifdef __cplusplus
}
#endif
