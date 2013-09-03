using System;
using System.Diagnostics;
using System.Linq;
using System.Security;

namespace iMDirectory.iSecurityComponent
{
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
	public class SimpleCrypto : IDisposable //XORFF Encryption (ISSpyra)
	{
		#region Private Variables
		private bool bDisposed;
		#endregion

		#region Constructors
		public SimpleCrypto()
		{
			this.bDisposed = false;
		}
		#endregion

		#region Public Methods
		public string Encrypt(string PlainText)
		{
			try
			{
				return Convert.ToBase64String(PlainText.ToCharArray().Select(cChar => (byte)(~Convert.ToByte(cChar))).ToArray());
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}

		public string Decrypt(string DecryptedText)
		{
			try
			{
				return new String(Convert.FromBase64String(DecryptedText).Select(bByte => Convert.ToChar((byte)~bByte)).ToArray());
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}
		#endregion

		#region Public Methods
		public static string EncryptString(string PlainText)
		{
			try
			{
				return Convert.ToBase64String(PlainText.ToCharArray().Select(cChar => (byte)(~Convert.ToByte(cChar))).ToArray());
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}

		public static string DecryptString(string DecryptedText)
		{
			try
			{
				return new String(Convert.FromBase64String(DecryptedText).Select(bByte => Convert.ToChar((byte)~bByte)).ToArray());
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, ex.Message));
			}
		}
		#endregion

		#region IDisposable Members
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool bDisposing)
		{
			if (!this.bDisposed)
			{
				if (bDisposing)
				{

				}

				this.bDisposed = true;
			}
		}

		#endregion
	}
}