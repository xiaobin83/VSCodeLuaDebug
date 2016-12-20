TOC

- 시작하기
- 고속 중단점 패치에 대해서
- 에러 발생시 디버거로 진입하기
- 폴링하기
- vscode-debuggee 레퍼런스 매뉴얼


------------------------------------------------



Lua Debugger를 가지고 루아 프로그램을 디버깅하기 위해서는
디버깅 대상이 될 프로그램에 vscode-debugee.lua 를 넣고 작동시켜야 합니다.
mobdebug를 써보신 분이라면 익숙하실 것입니다.

주의: luasocket이 필요합니다.

https://raw.githubusercontent.com/lee-seungjae/VSCodeLuaDebug/master/debugee/vscode-debuggee.lua 를 다운로드해서 프로젝트에 포함시키십시오.

    require 'json'
    local startResult, breakerType = (require 'vscode-debuggee').start(json)
    
    if startResult then
        print("startResult: " .. tostring(startResult))
        print("breakerType: " .. tostring(breakerType))
    end

이런 코드를 모든 소스코드가 로드된 이후에 실행시키십시오.

주의: json 테이블은 encode와 decode를 가져야 합니다.
이 코드는 Gideros 기준이므로, Gideros 가 아닌 경우 약간 수정할 필요가 있습니다.

주의: 이 코드가 실행된 이후에 로드된 코드에는 중단점을 설정할 수 없습니다.


디버거가 붙은 상태에서 실행되면 startResult가 true가 됩니다.

breakerType은 OP_HALT 패치가 되어서 고속 중단점을 사용할 수 있는 상태라면 ‘halt’,
그렇지 않은 상태라면 ‘pure’ 입니다.