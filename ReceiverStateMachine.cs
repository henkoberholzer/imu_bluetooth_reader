namespace hngkng.btgyro
{
	public class ReceiverStateMachine
	{
		private readonly System.Action<double> changeX;
		private readonly System.Action<double> changeY;
		private readonly System.Action<double> changeZ;

		private readonly System.Text.StringBuilder buffer;

		private bool stateX;
		private bool stateY;
		private bool stateZ;

		public ReceiverStateMachine(
			System.Action<double> changeX,
			System.Action<double> changeY,
			System.Action<double> changeZ)
		{
			this.buffer = new System.Text.StringBuilder(50);
			this.changeX = changeX;
			this.changeY = changeY;
			this.changeZ = changeZ;
		}

		private void Reset()
		{
			this.buffer.Clear();
			this.stateX = false;
			this.stateY = false;
			this.stateZ = false;
		}

		public void Receive(char next)
		{
			if (next == 'X')
			{
				this.Flush();
				this.Reset();
				this.stateX = true;
				return;
			}
			else if (next == 'Y')
			{
				this.Flush();
				this.Reset();
				this.stateY = true;
				return;
			}
			else if (next == 'Z')
			{
				this.Flush();
				this.Reset();
				this.stateZ = true;
				return;
			}

			if (!IsDigit(next))
			{
				this.Reset();
			};

			this.buffer.Append(next);
		}

		private static bool IsDigit(char next)
		{
			return
				next == '-' || next == '0' ||
				next == '1' || next == '2' || next == '3' ||
				next == '4' || next == '5' || next == '6' ||
				next == '7' || next == '8' || next == '9';
		}

		private void Flush()
		{
			if (this.buffer.Length == 0) return;

			double value = double.Parse(this.buffer.ToString());

			if (this.stateX)
			{
				this.changeX(value);
			}
			else if (this.stateY)
			{
				this.changeY(value);
			}
			else if (this.stateZ)
			{
				this.changeZ(value);
			}
		}
	}
}