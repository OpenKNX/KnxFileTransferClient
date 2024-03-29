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
|FileSize|Time|
|---|---|
|67 kB|2 min|
|100 kB| 3 min|
|150 kB| 4,5 min|
|200 kB| 6 min|
|250 kB| 7,5 min|
|300 kB| 9 min|

## Usage
You can use this console app on windows, linux or mac.  
The specified arguments are:  
>KnxFileTransferClient help

Will show you the arguments and its definition.

>KnxFileTransferClient <Command> <IP-Address> <PhysicalAddress> <Source?> <Target?> (--port=3671 --delay=0 --pkg=228)

|Argument|Definition|
|---|---|
|Command|Command to execute|
|IP-Address|IP of the KNX-IP-interface|
|PhysicalAddress|Address of the KNX-Device (ex. 1.2.120)|
|Source*|Path to the file on the host|
|Target**|Path to the file on the knx device|
|Port|Optional - Port of the KNX-IP-interface (default 3671)|
|Delay|Optional - Delay after each telegram|
|Package (pkg)|Optional - data size to transfer in one telegram (128 bytes)|
|Errors|Optional - Max count of errors before abort update|

>\*  only at command upload/download/delete  
>** only at command exists/rename/upload/download/list/mkdir/rmdir


|Command|Description|
|---|---|
|format|Format the filesystem|
|exists|Check if a file/dir exists|
|rename|Rename a file/dir|
|upload|Upload a file|
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