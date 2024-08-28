![Issues](https://badgen.net/github/open-issues/OpenKNX/KnxFileTransferClient)
![Branches](https://badgen.net/github/branches/OpenKNX/KnxFileTransferClient)
![Release](https://badgen.net/github/release/OpenKNX/KnxFileTransferClient)
[![CodeFactor](https://www.codefactor.io/repository/github/openknx/KnxFileTransferClient/badge)](https://www.codefactor.io/repository/github/openknx/KnxFileTransferClient)

# KnxFileTransferClient

This console app is uploading/downloading files to the filesystem of your knx-device.  
It can also list all content of a directory or create/delete one.  


## Requirements
You will have to use the [OFM-FileTransferModule](https://github.com/OpenKNX/OFM-FileTransferModule).  

## Speed
On an empty Bus we can get up to 570 Bytes/s.  
On a normal Bus we may get about 400 Bytes/s.  

|FileSize|Time 570 B/s|Time 400 B/s|
|---|---|---|
|67 kB|2 min|3 min|
|100 kB| 3 min|4 min|
|150 kB| 4,5 min|6 min|
|200 kB| 6 min|8 min|
|250 kB| 7,5 min|10 min|
|300 kB| 9 min|12,5 min|

## Usage
You can use this console app on windows, linux or mac.  
The specified arguments are:  
>KnxFileTransferClient help

Will show you the arguments and its definition.

>KnxFileTransferClient <Command!> <Source?> <Target?> (optional arguments)

|Argument|Definition|
|---|---|
|Command|Command to execute - Required|
|Source*|Path to the file on the host|
|Target**|Path to the file on the knx device|

>\*  only at command upload/download/delete/fwupdate   
>** only at command exists/rename/upload/download/list/mkdir/rmdir


|Command|Description|
|---|---|
|help|Show Help|
|format|Format the filesystem|
|exists|Check if a file/dir exists|
|rename|Rename a file/dir|
|upload|Upload a file|
|fwupdate|Upload a Firmware Update and Execute|
|download|Download a file|
|delete|Delete a file|
|list|List dir content|
|mkdir|Create dir|
|rmdir|Delete dir|
|open|Open Session|
|close|Close Session|

You can start a session where you can set multiple commands manually so you don't have to reconnect each time.  

>Open  = Session Start  
>Close = Session End

Optional Arguments:
|Argument|Description|Default|
|---|---|---|
|pkg|Size of Telegrams to use.|128|
|delay|Delay in ms between Telegrams to reduce busload|0|
|connect|Connection Type: Tunneling, Routing, Auto, Search|Search|
|verbose|Print the Stacktrace of Exceptions|-Boolean-|
|pa|Physikal Address of Target|1.1.255|
|port|Port of the Interface|3761|
|gw|IP of Gateway (or Multicast IP)|192.168.178.2|
|gs|PA Source Address (only Routing, PA have to be in KNX-TP Line)|0.0.1|
|config|Name of Configuration to save or load|default|
|interactive|All Arguments have to be verified by user|-Boolean-|
|force|Ignore Version warnings and force fwupdate|-Boolean-|

>Boolean type: not set=false; set=true

Example:
>KnxFileTransferClient fwupdate ./firmware.uf2 --connect Tunneling --gw 192.168.178.254 --pa 1.1.100 --config MDT --verbose


## Full Uninstall
This is a list with folders where this applications saves files  
  
Windows:
 - C:\Users\[UserName]\AppData\Local\KnxFileTransferClient\  
  
Unix:
 - $HOME/.local/share  