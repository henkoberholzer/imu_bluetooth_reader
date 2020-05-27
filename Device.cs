namespace hjo.btgyro
{
	using System.Linq;
	using Windows.Devices.Bluetooth;
	using Windows.Devices.Bluetooth.GenericAttributeProfile;
	using Windows.Devices.Enumeration;
	using Windows.Foundation;
	using Windows.Storage.Streams;

	public class Device
	{
		private readonly System.Action<string> log;
		private readonly System.Action<string> logEol;
		private readonly Nefarius.ViGEm.Client.ViGEmClient vigemClient;
		private readonly Nefarius.ViGEm.Client.Targets.IDualShock4Controller dualShock4Controller;
		private readonly Receiver receiver;

		public Device(
			System.Action<string> log,
			System.Action<string> logWithEol)
		{
			this.log = log;
			this.logEol = logWithEol;

			this.receiver = new NullReceiver();

			if (!this.BluetoothSetup())
			{
				throw new System.Exception("Could not connect to bluetooth device. Make sure it is paired and connected.");
			}

			this.vigemClient = new Nefarius.ViGEm.Client.ViGEmClient();
			this.dualShock4Controller = this.vigemClient.CreateDualShock4Controller();
			this.dualShock4Controller.Connect();

			this.receiver = new ReceiverStateMachine(this.SetX, this.SetY, this.SetZ);
		}

		public void Teardown()
		{
			this.dualShock4Controller.Disconnect();
			this.vigemClient.Dispose();
		}

		private static float scaleValue = 360f / 127.5f;
		private int lastX, lastY, lastZ;
		private int biasX, biasY, biasZ;

		public void Calibrate()
		{
			this.biasX = this.lastX * -1;
			this.biasY = this.lastY * -1;
			this.biasZ = this.lastZ * -1;
		}

		private static byte GetValueForDs4(int value, int bias)
		{
			int calibrated = value + bias;
			int abs = System.Math.Abs(calibrated);
			float scaled = abs / scaleValue;
			float ds4value = 127.5f + scaled;
			byte ds4valueAsByte = (byte)ds4value;
			return ds4valueAsByte;
		}

		private void SetX(int value)
		{
			this.lastX = value;
			byte processed = GetValueForDs4(value, this.biasX);
			this.dualShock4Controller.SetAxisValue(
				Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.RightThumbX,
				processed);
		}

		private void SetY(int value)
		{
			this.lastY = value;
			byte processed = GetValueForDs4(value, this.biasY);
			this.dualShock4Controller.SetAxisValue(
				Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.RightThumbY,
				processed);
		}

		private void SetZ(int value)
		{
			this.lastZ = value;
			byte processed = GetValueForDs4(value, this.biasZ);
			this.dualShock4Controller.SetAxisValue(
				Nefarius.ViGEm.Client.Targets.DualShock4.DualShock4Axis.LeftThumbX,
				processed);
		}

		private bool BluetoothSetup()
		{
			BluetoothLEDevice device = this.Waitfor(
				"connection to Bluetooth device",
				BluetoothLEDevice.FromBluetoothAddressAsync(System.Convert.ToUInt64("001580912553", 16)));

			if (device == null)
			{
				this.logEol("Device wasn't found");
				return false;
			}

			GattDeviceServicesResult deviceServices = this.Waitfor(
				"device services", device.GetGattServicesAsync());

			DeviceAccessStatus deviceAccessStatus = this.Waitfor(
				"device access", device.RequestAccessAsync());

			this.logEol($"Device access status: {deviceAccessStatus}");

			System.Guid gyroServiceGuid = System.Guid.Parse("0000ffe0-0000-1000-8000-00805f9b34fb");
			GattDeviceService gyroService = deviceServices.Services.Single(x => x.Uuid.Equals(gyroServiceGuid));

			var gyroServiceAccessStatus = this.Waitfor(
				"gro data service access", gyroService.RequestAccessAsync());

			this.logEol($"Gyro service access status: {gyroServiceAccessStatus}");

			GattCharacteristicsResult characteristics = this.Waitfor(
				"gyro data service", gyroService.GetCharacteristicsAsync());

			GattCharacteristic txRx = characteristics
				.Characteristics
				.SingleOrDefault(x => x.UserDescription.Replace(" ", "") == "TX&RX");

			if (txRx == default(GattCharacteristic))
			{
				this.logEol("Couldn't find TXRX channel...disconnected?");
				return false;
			}

			txRx.ValueChanged += this.TxRx_ValueChanged;
			return true;
		}

		private void TxRx_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
		{
			IBuffer buffer = args.CharacteristicValue;
			using (var dataReader = DataReader.FromBuffer(buffer))
			{
				string received = dataReader.ReadString(buffer.Length);
				foreach (char x in received)
				{
					this.receiver.Receive(x);
				}
			}
		}

		private T Waitfor<T>(string description, IAsyncOperation<T> asyncOperation)
		{
			this.log($"Waiting for {description}");
			while (asyncOperation.Status == AsyncStatus.Started) { }

			if (asyncOperation.Status == AsyncStatus.Completed)
			{
				this.logEol("...done");
				return asyncOperation.GetResults();
			}

			if (asyncOperation.Status == AsyncStatus.Error)
			{
				throw new System.Exception($"Error while waiting for {description}. Code: {asyncOperation.ErrorCode}");
			}
			if (asyncOperation.Status == AsyncStatus.Canceled)
			{
				throw new System.Exception($"Error while waiting for {description}. Cancellation was received.");
			}

			throw new System.Exception($"Unknown status code '{asyncOperation.Status}' while waiting for {description}");
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		private struct Data
		{
			public float Yaw;
			public float Pitch;
			public float Roll;

			public float X;
			public float Y;
			public float Z;
		}

		[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
		[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
		private static extern bool SetDllDirectory(string lpPathName);

		[System.Runtime.InteropServices.DllImport("freepie_io.dll")]
		private static extern int freepie_io_6dof_slots();

		[System.Runtime.InteropServices.DllImport("freepie_io.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		private static extern int freepie_io_6dof_write(int index, int length, Data[] data);

		private const int WriteToIndex = 0;
	}
}