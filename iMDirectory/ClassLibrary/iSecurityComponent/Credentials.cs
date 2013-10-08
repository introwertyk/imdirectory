using System;
using System.Diagnostics;
using System.Linq;
using System.Security;

namespace iMDirectory.iSecurityComponent
{
	/// <summary>
	/// Credentials class created to securely store and pass credentials between components and modules.
	/// </summary>
	public class Credentials : IDisposable
	{
		#region Variables
		private SecureString secPassword;
		private SecureString secUserName;

		private string sServerName;

		public string Password
		{
			get
			{
				return this.getSecureString(this.secPassword);
			}
			set
			{
				this.secPassword = this.setSecureString(value);
			}
		}

		public string UserName
		{
			get
			{
				return this.getSecureString(this.secUserName);
			}
			set
			{
				this.secUserName = this.setSecureString(value);
			}
		}

		public string ServerName
		{
			get
			{
				return this.sServerName;
			}
			set
			{
				this.sServerName = value;
			}
		}
		#endregion

		#region Constructors
		public Credentials()
		{
			;
		}
		public Credentials(string Password, string UserName, string ServerName)
		{
			this.Password = Password;
			this.UserName = UserName;
			this.sServerName = ServerName;
		}
		public Credentials(string Password, string UserName) : this(Password, UserName, null) { }
		#endregion

		#region Methods
		private SecureString setSecureString(string String)
		{
			SecureString secString = null;
			if (String == null)
			{
				return secString;
			}
			else
			{
				try
				{
					secString = new SecureString();

					foreach (Char cChar in String.ToCharArray())
					{
						secString.AppendChar(cChar);
					}

					secString.MakeReadOnly();

					return secString;
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}
		}
		private string getSecureString(SecureString String)
		{
			if (String == null)
			{
				return null;
			}
			else
			{
				try
				{
					IntPtr pointerName = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(String);

					try
					{
						return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(pointerName);
					}
					finally
					{
						System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(pointerName);
					}
				}
				catch (Exception eX)
				{
					throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
				}
			}
		}
		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			try
			{
				if (this.secPassword != null) this.secPassword.Dispose();
				if (this.secUserName != null) this.secUserName.Dispose();
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}
		#endregion
	}
}
