﻿using ForzaDSX.Config;
using ForzaDSX.GameParsers;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace ForzaDSX
{
	public enum InstructionTriggerMode : sbyte
	{
		NONE,
		RESISTANCE,
		VIBRATION
	}

	public class ForzaDSXWorker
	{
		public struct ForzaDSXReportStruct
		{
			public enum ReportType : ushort
			{
				VERBOSEMESSAGE = 0,
				NORACE = 1,
				RACING = 2
			}

			public enum RacingReportType : ushort
			{
				// 0 = Throttle vibration message
				THROTTLE_VIBRATION = 0,
				// 1 = Throttle message
				THROTTLE,
				// 2 = Brake vibration message
				BRAKE_VIBRATION,
				// 3 = Brake message
				BRAKE
			}
            public ForzaDSXReportStruct(VerboseLevel level, ReportType type, RacingReportType racingType, string msg)
            {
              this.verboseLevel = level;
				this.type = type;
                this.racingType = racingType;
                this.message = msg;
            }
            public ForzaDSXReportStruct(ReportType type, RacingReportType racingType, string msg)
			{
				this.type = type;
				this.racingType = racingType;
				this.message = msg;
			}

            public ForzaDSXReportStruct(VerboseLevel level, ReportType type, string msg)
            {
				this.verboseLevel = level;
                this.type = type;
                this.message = msg;
            }

            public ForzaDSXReportStruct(ReportType type, string msg)
			{
				this.type = type;
				this.message = msg;
			}
            public ForzaDSXReportStruct(VerboseLevel level, string msg)
            {
				this.verboseLevel = level;
                this.type = ReportType.VERBOSEMESSAGE;
                this.message = String.Empty;
            }

            public ForzaDSXReportStruct(string msg)
			{
				this.type = ReportType.VERBOSEMESSAGE;
				this.message = String.Empty;
			}

			public ReportType type = 0;
			public RacingReportType racingType = 0;
			public string message = string.Empty;
			public VerboseLevel verboseLevel = VerboseLevel.Limited;
		}

		ForzaDSX.Config.Config settings;
		IProgress<ForzaDSXReportStruct> progressReporter;
		Parser parser;





		public ForzaDSXWorker(ForzaDSX.Config.Config currentSettings, IProgress<ForzaDSXReportStruct> progressReporter)
		{
			settings = currentSettings;
			this.progressReporter = progressReporter;
		}

		public void SetSettings(ForzaDSX.Config.Config currentSettings)
		{
			lock(this)
			{
				settings = currentSettings;
			}
		}

		//This sends the data to DSX based on the input parsed data from Forza.
		//See DataPacket.cs for more details about what forza parameters can be accessed.
		//See the Enums at the bottom of this file for details about commands that can be sent to DualSenseX
		//Also see the Test Function below to see examples about those commands
		void SendData()
		{
			Packet p = new Packet();
	//		Parser parser = new ForzaParser(settings);
			CsvData csvRecord = new CsvData();
			Profile activeProfile = settings.ActiveProfile;
			ReportableInstruction reportableInstruction = new ReportableInstruction();
		

			// No race = normal triggers
			if (!parser.IsRaceOn())
			{
			

				reportableInstruction = parser.GetPreRaceInstructions();
				p.instructions = reportableInstruction.Instructions;
				reportableInstruction.ForzaDSXReportStructs.ForEach(x =>
				{

					if (x.verboseLevel <= settings.VerboseLevel
											&& progressReporter != null)
					{
						progressReporter.Report(x);
					}
				});

				//Send the commands to DSX
				Send(p);
			}
			else
			{
				reportableInstruction = parser.GetInRaceRightTriggerInstruction();
				sendReportableInstruction(reportableInstruction);
				reportableInstruction = parser.GetInRaceLeftTriggerInstruction();
				sendReportableInstruction(reportableInstruction);
				reportableInstruction = parser.GetInRaceLightbarInstruction();
				sendReportableInstruction(reportableInstruction);

			}
		}

		private void sendReportableInstruction(ReportableInstruction reportableInstruction)
		{
            reportableInstruction.ForzaDSXReportStructs.ForEach(x =>
			{

                if (x.verboseLevel <= settings.VerboseLevel
				                                        && progressReporter != null)
				{
                    progressReporter.Report(x);
                }
            });
			Packet p = new Packet();
			p.instructions = reportableInstruction.Instructions;
            //Send the commands to DSX
            Send(p);
        }

		//Maps floats from one range to another.
	

		//private DataPacket data;
		static UdpClient senderClient;
		static IPEndPoint endPoint;

		// Connect to DSX
		void Connect()
		{
			senderClient = new UdpClient();
			var portNumber = settings.DSXPort;

			if (progressReporter != null)
			{
				progressReporter.Report(new ForzaDSXReportStruct("DSX is using port " + portNumber + ". Attempting to connect.."));
			}

			int portNum;

			if (!int.TryParse(portNumber.ToString(), out portNum))
			{
				// handle parse failure
			}
			{
				if (progressReporter != null)
				{
					progressReporter.Report(new ForzaDSXReportStruct($"DSX provided a non-numerical port! Using configured default ({settings.DSXPort})."));
				}
				portNum = settings.DSXPort;
			}

			endPoint = new IPEndPoint(Triggers.localhost, portNum);

			try
			{
				senderClient.Connect(endPoint);
			}
			catch (Exception e)
			{
				if (progressReporter != null)
				{
					progressReporter.Report(new ForzaDSXReportStruct("Error connecting: " + e.Message));

					if (e is SocketException)
					{
						progressReporter.Report(new ForzaDSXReportStruct("Couldn't access port. " + e.Message));
					}
					else if (e is ObjectDisposedException)
					{
						progressReporter.Report(new ForzaDSXReportStruct("Connection object closed. Restart the application."));
					}
					else
					{
						progressReporter.Report(new ForzaDSXReportStruct("Unknown error: " + e.Message));
					}
				}
			}
		}

		
		//Send Data to DSX
		void Send(Packet data)
		{
			if (settings.VerboseLevel > VerboseLevel.Limited
				&& progressReporter != null)
			{
				progressReporter.Report(new ForzaDSXReportStruct($"Converting Message to JSON" ));
			}
			byte[] RequestData = Encoding.ASCII.GetBytes(Triggers.PacketToJson(data));
			if (settings.VerboseLevel > VerboseLevel.Limited
				&& progressReporter != null)
			{
				progressReporter.Report(new ForzaDSXReportStruct($"{Encoding.ASCII.GetString(RequestData)}" ));
			}
			try
			{
				if (settings.VerboseLevel > VerboseLevel.Limited
					&& progressReporter != null)
				{
					progressReporter.Report(new ForzaDSXReportStruct($"Sending Message to DSX..." ));
				}

				senderClient.Send(RequestData, RequestData.Length);

				if (settings.VerboseLevel > VerboseLevel.Limited
					&& progressReporter != null)
				{
					progressReporter.Report(new ForzaDSXReportStruct($"Message sent to DSX" ));
				}
			}
			catch (Exception e)
			{
				if (progressReporter != null)
					progressReporter.Report(new ForzaDSXReportStruct("Error Sending Message: " ));

				if (e is SocketException)
				{
					if (progressReporter != null)
						progressReporter.Report(new ForzaDSXReportStruct("Couldn't Access Port. " + e.Message ));
					throw e;
				}
				else if (e is ObjectDisposedException)
				{
					if (progressReporter != null)
						progressReporter.Report(new ForzaDSXReportStruct("Connection closed. Restarting..."));
					Connect();
				}
				else
				{
					if (progressReporter != null)
						progressReporter.Report(new ForzaDSXReportStruct("Unknown Error: " + e.Message));
				}

			}
		}

		static IPEndPoint ipEndPoint = null;
		static UdpClient client = null;
		
		public struct UdpState
		{
			public UdpClient u;
			public IPEndPoint e;

			public UdpState(UdpClient u, IPEndPoint e)
			{
				this.u = u;
				this.e = e;
			}
		}

		protected bool bRunning = false;

		public void Run()
		{
			bRunning = true;
			try
			{
				Connect();
				if (settings.ActiveProfile == null)
				{
                    if (progressReporter != null)
					{
                        progressReporter.Report(new ForzaDSXReportStruct("No active profile selected. Exiting..."));
                    }
                    return;
                }
				parser = new ForzaParser(settings);
				//Connect to Forza
				ipEndPoint = new IPEndPoint(IPAddress.Loopback, settings.ActiveProfile.gameUDPPort);
				client = new UdpClient(settings.ActiveProfile.gameUDPPort);

				DataPacket data;
				byte[] resultBuffer;
				//UdpReceiveResult receive;

				//Main loop, go until killed
				while (bRunning)
				{
					//If Forza sends an update
					resultBuffer = client.Receive(ref ipEndPoint);
					if (resultBuffer == null)
						continue;
					//receive = await client.ReceiveAsync();
					if (settings.VerboseLevel > VerboseLevel.Limited
						&& progressReporter != null)
					{
						progressReporter.Report(new ForzaDSXReportStruct("recieved Message from Forza!"));
					}
					//parse data
					//var resultBuffer = receive.Buffer;
					if (!AdjustToBufferType(resultBuffer.Length))
					{
						//  return;
					}
					//data = parseDirtData(resultBuffer);
					parser.ParsePacket(resultBuffer);
					if (settings.VerboseLevel > VerboseLevel.Limited
						&& progressReporter != null)
					{
						progressReporter.Report(new ForzaDSXReportStruct("Data Parsed"));
					}

					//Process and send data to DSX
					SendData();
				}
			}
			catch (Exception e)
			{
				if (progressReporter != null)
				{
					progressReporter.Report(new ForzaDSXReportStruct("Application encountered an exception: " + e.Message));
				}
			}
			finally
			{
				Stop();
			}
		}

		public void Stop()
		{
			bRunning = false;

			if (settings.VerboseLevel > VerboseLevel.Off
					&& progressReporter != null)
			{
				progressReporter.Report(new ForzaDSXReportStruct($"Cleaning Up"));
			}

			if (client != null)
			{
				client.Close();
				client.Dispose();
			}
			if (senderClient != null)
			{
				senderClient.Close();
				senderClient.Dispose();
			}

			if (settings.VerboseLevel > VerboseLevel.Off)
			{
				progressReporter.Report(new ForzaDSXReportStruct($"Cleanup Finished. Exiting..."));
			}
		}



		DataPacket parseDirtData(byte[] packet)
		{
            DataPacket data = new DataPacket();
			//data.AccelerationX

			data.IsRaceOn = true;
			data.Power = 1;
			data.CurrentEngineRpm = PacketParse.GetSingle(packet, 148) * 10.0f;
			data.Speed = PacketParse.GetSingle(packet, 28);
			data.frontLeftContactPatchV = PacketParse.GetSingle(packet, 108);
            data.TireCombinedSlipFrontLeft = calcTireSlip(PacketParse.GetSingle(packet, 108), data.Speed);
			data.TireCombinedSlipFrontRight = calcTireSlip(PacketParse.GetSingle(packet, 112), data.Speed);
			data.TireCombinedSlipRearLeft = calcTireSlip(PacketParse.GetSingle(packet, 100), data.Speed);
			data.TireCombinedSlipRearRight = calcTireSlip(PacketParse.GetSingle(packet, 104), data.Speed);


            data.CarClass = 0;

			data.CarPerformanceIndex = 0;

                data.AccelerationX = PacketParse.GetSingle(packet, 136);

                data.AccelerationZ = PacketParse.GetSingle(packet, 140);

                data.Accelerator = (uint)(PacketParse.GetSingle(packet, 116)* 255.0f);

                data.Brake = (uint)(PacketParse.GetSingle(packet, 120) * 255.0f);

			data.EngineMaxRpm = PacketParse.GetSingle(packet, 252) * 10.0f;
			data.EngineIdleRpm = 0;
            return data;
        }
		static float calcTireSlip(float contactPatchSpeed, float vehicleSpeed)
		{
			if (Math.Abs(vehicleSpeed) < 0.1f)
			{
                return 0;
            }
			return 3.0f * (Math.Abs(Math.Abs(contactPatchSpeed) - vehicleSpeed) / vehicleSpeed);
		}
        //Parses data from Forza into a DataPacket
     /*   DataPacket ParseData(byte[] packet)
		{
			DataPacket data = new DataPacket();

			// sled
			data.IsRaceOn = packet.IsRaceOn();
			data.TimestampMS = packet.TimestampMs();
			data.EngineMaxRpm = packet.EngineMaxRpm();
			data.EngineIdleRpm = packet.EngineIdleRpm();
			data.CurrentEngineRpm = packet.CurrentEngineRpm();
			data.AccelerationX = packet.AccelerationX();
			data.AccelerationY = packet.AccelerationY();
			data.AccelerationZ = packet.AccelerationZ();
			data.VelocityX = packet.VelocityX();
			data.VelocityY = packet.VelocityY();
			data.VelocityZ = packet.VelocityZ();
			data.AngularVelocityX = packet.AngularVelocityX();
			data.AngularVelocityY = packet.AngularVelocityY();
			data.AngularVelocityZ = packet.AngularVelocityZ();
			data.Yaw = packet.Yaw();
			data.Pitch = packet.Pitch();
			data.Roll = packet.Roll();
			data.NormalizedSuspensionTravelFrontLeft = packet.NormSuspensionTravelFl();
			data.NormalizedSuspensionTravelFrontRight = packet.NormSuspensionTravelFr();
			data.NormalizedSuspensionTravelRearLeft = packet.NormSuspensionTravelRl();
			data.NormalizedSuspensionTravelRearRight = packet.NormSuspensionTravelRr();
			data.TireSlipRatioFrontLeft = packet.TireSlipRatioFl();
			data.TireSlipRatioFrontRight = packet.TireSlipRatioFr();
			data.TireSlipRatioRearLeft = packet.TireSlipRatioRl();
			data.TireSlipRatioRearRight = packet.TireSlipRatioRr();
			data.WheelRotationSpeedFrontLeft = packet.WheelRotationSpeedFl();
			data.WheelRotationSpeedFrontRight = packet.WheelRotationSpeedFr();
			data.WheelRotationSpeedRearLeft = packet.WheelRotationSpeedRl();
			data.WheelRotationSpeedRearRight = packet.WheelRotationSpeedRr();
			data.WheelOnRumbleStripFrontLeft = packet.WheelOnRumbleStripFl();
			data.WheelOnRumbleStripFrontRight = packet.WheelOnRumbleStripFr();
			data.WheelOnRumbleStripRearLeft = packet.WheelOnRumbleStripRl();
			data.WheelOnRumbleStripRearRight = packet.WheelOnRumbleStripRr();
			data.WheelInPuddleDepthFrontLeft = packet.WheelInPuddleFl();
			data.WheelInPuddleDepthFrontRight = packet.WheelInPuddleFr();
			data.WheelInPuddleDepthRearLeft = packet.WheelInPuddleRl();
			data.WheelInPuddleDepthRearRight = packet.WheelInPuddleRr();
			data.SurfaceRumbleFrontLeft = packet.SurfaceRumbleFl();
			data.SurfaceRumbleFrontRight = packet.SurfaceRumbleFr();
			data.SurfaceRumbleRearLeft = packet.SurfaceRumbleRl();
			data.SurfaceRumbleRearRight = packet.SurfaceRumbleRr();
			data.TireSlipAngleFrontLeft = packet.TireSlipAngleFl();
			data.TireSlipAngleFrontRight = packet.TireSlipAngleFr();
			data.TireSlipAngleRearLeft = packet.TireSlipAngleRl();
			data.TireSlipAngleRearRight = packet.TireSlipAngleRr();
			data.TireCombinedSlipFrontLeft = packet.TireCombinedSlipFl();
			data.TireCombinedSlipFrontRight = packet.TireCombinedSlipFr();
			data.TireCombinedSlipRearLeft = packet.TireCombinedSlipRl();
			data.TireCombinedSlipRearRight = packet.TireCombinedSlipRr();
			data.SuspensionTravelMetersFrontLeft = packet.SuspensionTravelMetersFl();
			data.SuspensionTravelMetersFrontRight = packet.SuspensionTravelMetersFr();
			data.SuspensionTravelMetersRearLeft = packet.SuspensionTravelMetersRl();
			data.SuspensionTravelMetersRearRight = packet.SuspensionTravelMetersRr();
			data.CarOrdinal = packet.CarOrdinal();
			data.CarClass = packet.CarClass();
			data.CarPerformanceIndex = packet.CarPerformanceIndex();
			data.DrivetrainType = packet.DriveTrain();
			data.NumCylinders = packet.NumCylinders();

			// dash
			data.PositionX = packet.PositionX();
			data.PositionY = packet.PositionY();
			data.PositionZ = packet.PositionZ();
			data.Speed = packet.Speed();
			data.Power = packet.Power();
			data.Torque = packet.Torque();
			data.TireTempFl = packet.TireTempFl();
			data.TireTempFr = packet.TireTempFr();
			data.TireTempRl = packet.TireTempRl();
			data.TireTempRr = packet.TireTempRr();
			data.Boost = packet.Boost();
			data.Fuel = packet.Fuel();
			data.Distance = packet.Distance();
			data.BestLapTime = packet.BestLapTime();
			data.LastLapTime = packet.LastLapTime();
			data.CurrentLapTime = packet.CurrentLapTime();
			data.CurrentRaceTime = packet.CurrentRaceTime();
			data.Lap = packet.Lap();
			data.RacePosition = packet.RacePosition();
			data.Accelerator = packet.Accelerator();
			data.Brake = packet.Brake();
			data.Clutch = packet.Clutch();
			data.Handbrake = packet.Handbrake();
			data.Gear = packet.Gear();
			data.Steer = packet.Steer();
			data.NormalDrivingLine = packet.NormalDrivingLine();
			data.NormalAiBrakeDifference = packet.NormalAiBrakeDifference();

			return data;
		}*/

		//Support different standards
		static bool AdjustToBufferType(int bufferLength)
		{
			switch (bufferLength)
			{
				case 232: // FM7 sled
					return false;
				case 311: // FM7 dash
					FMData.BufferOffset = 0;
					return true;
				case 331: // FM8 dash
					FMData.BufferOffset = 0;
					return true;
				case 324: // FH4
					FMData.BufferOffset = 12;
					return true;
				default:
					return false;
			}
		}
	}
}
