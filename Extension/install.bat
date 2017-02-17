set VERSION=1.0.8
set INSTALL_PATH=%HOMEDRIVE%%HOMEPATH%\.vscode\extensions\devCAT.lua-debug-%VERSION%
copy /y *.dll %INSTALL_PATH%
copy /y *.exe %INSTALL_PATH%
