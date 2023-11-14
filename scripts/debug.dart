import 'dart:io';

void main() async {
  const String rhinoExecutablePath = '/Applications/Rhino 7.app/Contents/MacOS/Rhinoceros';
  const String rhinoPluginsDirectory = '/Applications/Rhino 7.app/Contents/PlugIns';

  const String pluginAssemblyPath = './bin/Debug/net48/Winder.rhp';
  const String pluginAssemblyName = 'Winder.rhp';

  final String pluginDestinationPath = [rhinoPluginsDirectory, pluginAssemblyName].join(Platform.pathSeparator);

  await Process.run('dotnet', ['build']);
  
  await File(pluginAssemblyPath).copy(pluginDestinationPath);

  await Process.start(rhinoExecutablePath, ['-nosplash', '-new']);
}