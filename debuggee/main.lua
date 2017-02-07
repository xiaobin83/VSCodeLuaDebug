-- 유티에프-팔
require 'strict'
package.path = '?.lua;lua/?.lua;' .. package.path
local json = require 'dkjson'

local function onError(e)
	print('[onError]' .. e)
end

local debuggee = (require 'vscode-debuggee')
local startResult, breakerType = debuggee.start(json, { onError = onError })
print('debuggee.start(): ', tostring(startResult), breakerType)

local json = require 'dkjson'

local c = coroutine.create(function()
	local function r(i)
		if i > 0 then
			return r(i - 1) + 1
		else
			error('코루틴 중 에러내봄')
		end
	end
	r(10)	
end)
local coSuccess, coErrorMessage = coroutine.resume(c)
if not coSuccess then
	debuggee.enterDebugLoop(c, coErrorMessage)
end


local function d()
	local s = '하늘에서 정의가 빗발친다'
	local x = nil
	local t = {}
	t.itself = t

	for i = 1, 3 do
		print('Hello, World')
	end
end

local function c(...)
	(require 'c')(d)
end

local function b()
	c('a', 'b', 'c')
end

local function a()
	b()
	return 1
end

xpcall(
	function()
		local a = 1 + nil
	end,
	function(e)
		if debuggee.enterDebugLoop(1, e) then
			-- ok
		else
			print('(XPCALL)')
			print(e)
			print(debug.traceback())
		end
	end)

a()
print('-')
a()
print('-')
a()
print('-')
a()
print('-')
a()

local function createClosure(i)
	return function()
		print(i)
	end
end
local x = createClosure(42)
x()


local mt = { __index = {} }
local t = {}
setmetatable(t, mt)

print('end')
