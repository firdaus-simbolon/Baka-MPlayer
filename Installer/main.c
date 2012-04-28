#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#include <winsock.h>

const char default_location[] = "C:\\Program Files\\Baka MPlayer\\";
const char default_archiver[] = "\"C:\\Program Files\\7-Zip\\7z.exe\" x -y -o";
const char file_name[] = "Baka MPlayer.7z";
const char website_url[] = "bakamplayer.netii.net";
const char version_request[] = "GET /version HTTP/1.1\r\nHost: bakamplayer.netii.net\r\nConnection: Close\r\n\r\n";
const char program_request[] = "GET /Baka MPlayer.7z HTTP/1.1\r\nHost: bakamplayer.netii.net\r\n\r\n";

unsigned long find_host(const char *address);

int main(int argc,char *argv[])
{
  WSADATA            wsaData;
  FILE              *file;
  SOCKET             sock;
  struct sockaddr_in addr;
  char               location[256],
                     archive_location[256],
                     input[1],
                     *ptr;

  if(WSAStartup(MAKEWORD(2,0),&wsaData) == 0)
  {
    if(LOBYTE(wsaData.wVersion) < 2)
      return -1;
  }
  else
    return -1;

  SetConsoleTitle("Baka MPlayer Installer");

  printf(" ----------- \n< by u8sand >\n ----------- \n\n");

  printf("Preparing sockets...\n");
  sock = socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);
  addr.sin_family = AF_INET;
  addr.sin_port = htons(80);
  addr.sin_addr.S_un.S_addr = find_host(website_url);

  printf("Retreiving Version Information...\n");
  if(connect(sock,(struct sockaddr*)&addr,sizeof(addr)) == SOCKET_ERROR ||
     send(sock,version_request,strlen(version_request),0) == SOCKET_ERROR)
  {
    printf("Failed to retreive version information.\n");
    return -1;
  }
  while(recv(sock,input,1,0) > 0)
    putc(input[0],stdout);
  closesocket(sock);

  printf("\n\nInstall Location [%s]: ",default_location);
  gets(location);
  if(strcmp(location,"") == 0)
    strcpy(location,default_location);

  printf("\nArchive Location [%s]: ",default_archiver);
  gets(archive_location);
  if(strcmp(archive_location,"") == 0)
    strcpy(archive_location,default_archiver);

  printf("\nProceed with installation? [Y/n]: ");
  gets(input);
  if(!(input[0] == '\0' || input[0] == 'y' || input[0] == 'Y'))
    return 0;
  fflush(stdin);

  /* cleanaup sockets */
  memset(&sock,0,sizeof(sock));
  memset(&addr,0,sizeof(addr));

  printf("\nDownloading...");
  sock = socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);
  addr.sin_family = AF_INET;
  addr.sin_port = htons(80);
  addr.sin_addr.S_un.S_addr = find_host(website_url);
  if(connect(sock,(struct sockaddr*)&addr,sizeof(addr)) == SOCKET_ERROR ||
     send(sock,program_request,strlen(program_request),0) == SOCKET_ERROR)
  {
    printf("Failed to download.\n");
    return -1;
  }

  ptr = malloc(strlen(location)+strlen(file_name));
  strcpy(ptr,location);
  strcat(ptr,file_name);
  file = fopen(ptr, "w");
  while(recv(sock,input,1,0) > 0)
      fputc(input[0],file);
  fclose(file);
  free(ptr);

  printf("\nExtracting...\n");
  ptr = malloc(strlen(archive_location)+(strlen(location)*2)+strlen(file_name)+5);
  strcpy(ptr,archive_location);
  strcat(ptr,"\"");
  strcat(ptr,location);
  strcat(ptr,"\" \"");
  strcat(ptr,location);
  strcat(ptr,file_name);
  strcat(ptr,"\"");
  printf("%s\n\n",ptr);

  system(ptr);

  return 0;
}
unsigned long find_host(const char *address)
{
  struct hostent *pHostent;
  if((pHostent = gethostbyname(address)))
    if(pHostent->h_addr_list && pHostent->h_addr_list[0])
      return *(unsigned long*)pHostent->h_addr_list[0];
  return 0;
}
