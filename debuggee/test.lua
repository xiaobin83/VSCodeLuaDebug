-- 유티에프-팔
require 'strict'
package.path = '?.lua;lua/?.lua;bin/modules/?.lua' .. package.path
local json = require 'dkjson'

local function onError(e)
	print('[onError]' .. e)
end

local debuggee = (require 'vscode-debuggee')
local startResult, breakerType = debuggee.start(json, { onError = onError, redirectPrint = false })
print('debuggee.start(): ', tostring(startResult), breakerType)

print('a')
print('b')
print('c')
print('d')

print('a')
print('b')
print('c')
print('d')

