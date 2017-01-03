param($installPath, $toolsPath, $package, $project)
$configItem = $project.ProjectItems.Item("cryptominisat5_simple.exe")
$copyToOutput = $configItem.Properties.Item("CopyToOutputDirectory")
$copyToOutput.Value = 2
$buildAction = $configItem.Properties.Item("BuildAction")
$buildAction.Value = 2
