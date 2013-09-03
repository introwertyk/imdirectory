namespace iMDirectory
{
    partial class CycleController
	{
		#region Variables
		private System.Timers.Timer oTimeController;
		private System.ComponentModel.IContainer components = null;
		#endregion

		protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
			this.ServiceName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

			this.oTimeController = new System.Timers.Timer(60 * 1000);
			this.oTimeController.Enabled =	true;
			this.oTimeController.Elapsed += new System.Timers.ElapsedEventHandler(this.OnElapsed);
        }
        #endregion
    }
}
