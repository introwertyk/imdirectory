using System;
using System.Configuration;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;

[RunInstaller(true)]
public class ServiceInstallerClass : System.Configuration.Install.Installer
{
	private System.ComponentModel.Container components = null;

	public ServiceInstallerClass()
	{
		InitializeComponent();

		System.Reflection.Assembly objAssembly = System.Reflection.Assembly.GetExecutingAssembly();
		System.Reflection.AssemblyDescriptionAttribute objDesc = (System.Reflection.AssemblyDescriptionAttribute)System.Reflection.AssemblyDescriptionAttribute.GetCustomAttribute(objAssembly, typeof(System.Reflection.AssemblyDescriptionAttribute));
		System.Reflection.AssemblyTitleAttribute objTitle = (System.Reflection.AssemblyTitleAttribute)System.Reflection.AssemblyTitleAttribute.GetCustomAttribute(objAssembly, typeof(System.Reflection.AssemblyTitleAttribute));

		ServiceProcessInstaller ServiceProcess = new ServiceProcessInstaller();
		ServiceProcess.Account = ServiceAccount.LocalSystem;

		ServiceInstaller ResourceInstaller = new ServiceInstaller();
		ResourceInstaller.ServiceName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
		Type aType = typeof(System.Reflection.AssemblyTitleAttribute);
		ResourceInstaller.DisplayName = objTitle.Title;
		aType = typeof(System.Reflection.AssemblyDescriptionAttribute);
		ResourceInstaller.Description = objDesc.Description;
		ResourceInstaller.StartType = ServiceStartMode.Automatic;

		Installers.Add(ResourceInstaller);
		Installers.Add(ServiceProcess);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (components != null)
			{
				components.Dispose();
			}
		}
		base.Dispose(disposing);
	}

	#region Component Designer generated code
	private void InitializeComponent()
	{
		components = new System.ComponentModel.Container();
	}
	#endregion
}
