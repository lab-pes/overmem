local function loadRepoPaths()
  if type(_G.__my_cheat_tables_repo_paths) == "table" then
    return _G.__my_cheat_tables_repo_paths
  end

  local function normalize(path)
    local text = tostring(path or ""):gsub("/", "\\")
    return text:gsub("\\+", "\\")
  end

  local function dirname(path)
    return normalize(path):match("^(.*)\\[^\\]+$")
  end

  local function fileExists(path)
    local handle = io.open(path, "rb")
    if handle then
      handle:close()
      return true
    end
    return false
  end

  local source = debug.getinfo(1, "S").source or ""
  if source:sub(1, 1) == "@" then
    local currentDir = dirname(source:sub(2))
    local calendarDir = dirname(currentDir or "")
    local scriptsDir = dirname(calendarDir or "")
    local candidates = {}

    if scriptsDir then
      candidates[#candidates + 1] = normalize(scriptsDir .. "\\support\\repo_paths.lua")
    end

    for _, candidate in ipairs(candidates) do
      if fileExists(candidate) then
        local loader = assert(loadfile(candidate))
        local loaded = loader()
        if type(loaded) == "table" then
          _G.__my_cheat_tables_repo_paths = loaded
          return loaded
        end
      end
    end
  end

  error("Nao foi possivel localizar scripts\\support\\repo_paths.lua a partir do bootstrap do cheat table")
end

local repoPaths = loadRepoPaths()

local function loadLuaFile(path, label)
  local loader, loadErr = loadfile(path)
  if not loader then
    error(string.format("%s: falha ao carregar %s: %s", tostring(label), tostring(path), tostring(loadErr)))
  end

  local ok, resultOrErr = pcall(loader)
  if not ok then
    error(string.format("%s: falha ao executar %s: %s", tostring(label), tostring(path), tostring(resultOrErr)))
  end

  return resultOrErr
end

function myCheatTablesResolveScript(relativePath)
  return repoPaths.join(repoPaths.scriptsDir, relativePath)
end

function myCheatTablesLoadLuaScript(relativePath)
  local fullPath = myCheatTablesResolveScript(relativePath)
  return loadLuaFile(fullPath, "Cheat table external script")
end

function mlCalendarEnsureInspectorLoaded()
  if type(findMLArrayBySeasonAnchor) == "function" and type(getMLArrayBase) == "function" then
    return true
  end

  loadLuaFile(repoPaths.calendarScript("PES2021_MLCalendarInspector.lua"), "ML Calendar bootstrap")
  return type(findMLArrayBySeasonAnchor) == "function" and type(getMLArrayBase) == "function"
end

function mlCalendarTryResolveBase()
  local seasonAnchorYears = { 2026, 2025, 2027, 2024, 2028, 2023, 2029 }

  if not mlCalendarEnsureInspectorLoaded() then
    return 0
  end

  local currentBase = getMLArrayBase()
  if currentBase and currentBase ~= 0 then
    return currentBase
  end

  for _, year in ipairs(seasonAnchorYears) do
    local ok = findMLArrayBySeasonAnchor(year)
    if ok then
      currentBase = getMLArrayBase()
      if currentBase and currentBase ~= 0 then
        return currentBase
      end
    end
  end

  return 0
end

return {
  repoPaths = repoPaths,
}