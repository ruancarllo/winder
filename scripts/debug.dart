import 'dart:io';

void main() async {
  if (Platform.isWindows) {
    final String currentUserName = Platform.script.toFilePath().split(Platform.pathSeparator)[2];

    String? rhinocerosExecutablePath;
    String? rhinocerosPluginsDirectory;

    if (await File('C:\\Program Files\\Rhino 7\\System\\Rhino.exe').exists()) {
      rhinocerosExecutablePath = 'C:\\Program Files\\Rhino 7\\System\\Rhino.exe';
      rhinocerosPluginsDirectory = 'C:\\Users\\$currentUserName\\AppData\\Roaming\\McNeel\\Rhinoceros\\7.0\\Plug-ins';
    }

    if (await File('C:\\Program Files\\Rhinoceros 5 (64-bit)\\System\\Rhino.exe').exists()) {
      rhinocerosExecutablePath = 'C:\\Program Files\\Rhinoceros 5 (64-bit)\\System\\Rhino.exe';
      rhinocerosPluginsDirectory = 'C:\\Users\\$currentUserName\\AppData\\Roaming\\McNeel\\Rhinoceros\\5.0\\Plug-ins';
    }

    const String pluginComplementaryDirectory = 'Winder (039b4c68-60fb-4b38-8f1f-e9e093618bc6)\\1.0.0.0';

    const String pluginAssemblyPath = '.\\bin\\Debug\\net48\\Winder.rhp';
    const String pluginAssemblyName = 'Winder.rhp';

    final String pluginDestinationPath = [rhinocerosPluginsDirectory, pluginComplementaryDirectory, pluginAssemblyName].join(Platform.pathSeparator);

    if (rhinocerosExecutablePath != null && rhinocerosPluginsDirectory != null) {
      await Process.run('dotnet', ['build']);

      if (!await File(pluginDestinationPath).parent.exists()) {
        await File(pluginDestinationPath).parent.create(recursive: true);
      }

      await File(pluginAssemblyPath).copy(pluginDestinationPath);

      await Process.start(rhinocerosExecutablePath, ['/nosplash', '/new', '.\\mocks\\example.3dm']);
    }
  }

  if (Platform.isMacOS) {
    const String rhinocerosExecutablePath = '/Applications/Rhino 7.app/Contents/MacOS/Rhinoceros';
    const String rhinocerosPluginsDirectory = '/Applications/Rhino 7.app/Contents/PlugIns';

    const String pluginAssemblyPath = './bin/Debug/net48/Winder.rhp';
    const String pluginAssemblyName = 'Winder.rhp';

    final String pluginDestinationPath = [rhinocerosPluginsDirectory, pluginAssemblyName].join(Platform.pathSeparator);

    await Process.run('dotnet', ['build']);

    if (!await Directory(pluginDestinationPath).parent.exists()) {
      await Directory(pluginDestinationPath).parent.create(recursive: true);
    }

    await File(pluginAssemblyPath).copy(pluginDestinationPath);

    await Process.start(rhinocerosExecutablePath, ['-nosplash', '-new', './mocks/example.3dm']);
  }
}