import 'dart:io';

void main() async {
  if (Platform.isWindows) {
    final String currentUserName = Platform.script.toFilePath().split(Platform.pathSeparator)[2];

    const String rhinoExecutablePath = 'C:\\Program Files\\Rhino 7\\System\\Rhino.exe';
    final String rhinoPluginsDirectory = 'C:\\Users\\$currentUserName\\AppData\\Roaming\\McNeel\\Rhinoceros\\7.0\\Plug-ins';

    const String pluginDirectoryComplement = 'Winder (039b4c68-60fb-4b38-8f1f-e9e093618bc6)\\1.0.0.0';

    const String pluginAssemblyPath = '.\\bin\\Debug\\net48\\Winder.rhp';
    const String pluginAssemblyName = 'Winder.rhp';

    final String pluginDestinationPath = [rhinoPluginsDirectory, pluginDirectoryComplement, pluginAssemblyName].join(Platform.pathSeparator);

    await Process.run('dotnet', ['build']);

    if (!await File(pluginDestinationPath).parent.exists()) {
      await File(pluginDestinationPath).parent.create(recursive: true);
    }

    await File(pluginAssemblyPath).copy(pluginDestinationPath);

    await Process.start(rhinoExecutablePath, ['/nosplash', '/new']);
  }

  if (Platform.isMacOS) {
    const String rhinoExecutablePath = '/Applications/Rhino 7.app/Contents/MacOS/Rhinoceros';
    const String rhinoPluginsDirectory = '/Applications/Rhino 7.app/Contents/PlugIns';

    const String pluginAssemblyPath = './bin/Debug/net48/Winder.rhp';
    const String pluginAssemblyName = 'Winder.rhp';

    final String pluginDestinationPath = [rhinoPluginsDirectory, pluginAssemblyName].join(Platform.pathSeparator);

    await Process.run('dotnet', ['build']);

    if (!await Directory(pluginDestinationPath).exists()) {
      await Directory(pluginDestinationPath).create(recursive: true);
    }

    await File(pluginAssemblyPath).copy(pluginDestinationPath);

    await Process.start(rhinoExecutablePath, ['-nosplash', '-new']);
  }
}