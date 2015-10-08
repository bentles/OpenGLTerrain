using System;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.Collections.Generic;
using SimplexNoise;

namespace Enviro3D
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Game game = new Game();
			game.Run(60, 60);
		}
	}

	class Game : GameWindow
	{
		float etime = 0;
        int updateCountdown = 30;
        int updates = 0;

		Vector3 eye = new Vector3(0,60,0);
		Vector3 target = new Vector3(0,60,1);

		float move_speed = 50f;
		float mouse_speed = 0.07f;

		bool movesun = false;
		bool flat = false;
		bool downhills = false;
		bool paused = true;

		const float LEFT = -1;
		const float RIGHT = 1;
		const float TOP = 1;
		const float BOTTOM = -1;

		const bool ADD = true;
		const bool MUL = false;

		float[] lightPos;

		Vector2 oldmouse = Vector2.Zero;
		Terrain t;

		void Init()
		{
			Title = "Enviro 3D";
			WindowState = WindowState.Fullscreen;
			CursorVisible = false;
			GL.ClearColor(Color.FromArgb(0xC0D9FA));

			lightPos = new float[]{ 10f, 10.0f, 10.0f, 1.0f };		

			GL.Enable(EnableCap.Light0);
			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.CullFace);
			GL.Enable(EnableCap.DepthTest);
			//GL.Enable(EnableCap.Blend);
			//GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.ShadeModel(ShadingModel.Smooth);

			//make terain from composed random noise
			List<NoiseWrapper> noisy = new List<NoiseWrapper> {
				new NoiseWrapper(2f, 0.011f, ADD),
				new NoiseWrapper(2f, 0.004f, ADD),
				new NoiseWrapper(1.5f, 0.009f, MUL),
				new NoiseWrapper(2f, 0.04f, ADD),
				//new NoiseWrapper(2f, 0.004f, ADD),
				new NoiseWrapper(0.2f, 0.71f, ADD),
				new NoiseWrapper(2f, 0.004f, ADD),
				new NoiseWrapper(2f, 0.004f, ADD),
				new NoiseWrapper(0.5f, 0.15f, ADD),
				new NoiseWrapper(1.5f, 0.009f, MUL)
				};
			
			t = new Terrain(150,150, 1f, noisy);
			t.GenerateTerrainFromNoise();
		}

		void Draw(Terrain terrain)
		{
			//enable light
			float diffuseLight = 0.8f;
			float ambientLight = 0.6f;
			float specularLight = 0.6f;

			float[] lightKa = { ambientLight, ambientLight, ambientLight, 1.0f }; 
			float[] lightKd = { diffuseLight, diffuseLight, diffuseLight, 1.0f }; 
			float[] lightKs = { specularLight, specularLight, specularLight, 0.06f };    

			GL.Light(LightName.Light0, LightParameter.Ambient, lightKa);
			GL.Light(LightName.Light0, LightParameter.Diffuse, lightKd);
			GL.Light(LightName.Light0, LightParameter.Specular, lightKs);
			GL.Light(LightName.Light0, LightParameter.Position, lightPos);

			terrain.Draw(flat, downhills);
		}

		protected override void OnLoad (EventArgs e)
		{
			base.OnLoad (e);
			Init();
		}

		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);
			etime += (float)e.Time;

			if (!paused)
			{
	            //update water
	            if (--updateCountdown == 0)
	            {
	                updateCountdown = 5;
	                t.UpdateState(updates++);
	            }
			}

			//move
			Vector3 forward = (target - eye); //should be a unit vectorial
			forward.Y = 0;
			Vector3 left = new Vector3(forward.Z, 0, -forward.X);
			Vector3 step = Vector3.Zero;

			KeyboardState kb = OpenTK.Input.Keyboard.GetState();
			if (kb[Key.W])
				step += forward;
			if (kb[Key.S])
				step += - forward;
			if (kb[Key.A])
				step += left;
			if (kb[Key.D])
				step += -left;
			if (kb[Key.Escape])
				Close();
			if (kb[Key.Space])
				step = Vector3.UnitY;
			if (kb[Key.ControlLeft])
				step = -Vector3.UnitY;
		

			if (step != Vector3.Zero)
				step.Normalize();	

			step *= move_speed*(float)e.Time;

			eye += step;
			target += step;

			//look
			MouseState ms = OpenTK.Input.Mouse.GetState();
			Vector2 mouse = new Vector2(ms.X, ms.Y);
			float mouse_ZX = (oldmouse - mouse).X; //how far the mouse moved sideways
			float mouse_UD = (mouse - oldmouse).Y; //how far the mouse moved up and down
			Matrix4 rotate = Matrix4.CreateRotationY(mouse_ZX * mouse_speed * (float)e.Time);
			Matrix4 rotateUD = Matrix4.CreateFromAxisAngle(left , mouse_UD * mouse_speed * (float)e.Time);
			oldmouse = mouse;

			target -= eye;
			target = Vector3.Transform(target, rotate);
			Vector3 ud = Vector3.Transform(target, rotateUD);

			if (Vector3.Dot(ud, forward) > 0)
		    	target = ud;

			target += eye;


			//lighting
			if (movesun)
			{
				lightPos[0] = 50 + (float)Math.Cos(etime / 6)*300 ;
				lightPos[1] = 100;
				lightPos[2] = 50 + (float)Math.Sin(etime / 6)*300 ;
			}
			else
			{
				lightPos[0] = 50 ;
				lightPos[1] = 100;
				lightPos[2] = 50 ;
			}
		}

		protected override void OnRenderFrame (FrameEventArgs e)
		{
			base.OnRenderFrame (e);
			//select some bits from a bitmask that represent what we want to clear I guess
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			//create a perspective projection
			Matrix4 perspective = Matrix4.CreatePerspectiveFieldOfView(0.9f, ClientRectangle.Width/(float)ClientRectangle.Height, 0.01f, 1000f);
			//tell openGL we want to set the Projection matrix
			GL.MatrixMode(MatrixMode.Projection);
			//set the projection matrix
			GL.LoadMatrix(ref perspective);

			//builds a world space to camera space matrix up is <0,1,0>
			Matrix4 modelview = Matrix4.LookAt(eye, target, Vector3.UnitY);
			//tell openGL we want to set the modelview matrix to this matrix
			GL.MatrixMode(MatrixMode.Modelview);
			//set it
			GL.LoadMatrix(ref modelview);

			//these coords in world space are multiplied by the world space -> camera matrix to provide something we can see
			//so this is where we want the drawing code to go
			Draw(t);

			SwapBuffers();
		}

		protected override void OnKeyDown (KeyboardKeyEventArgs e) {
			base.OnKeyDown (e);
			if (e.Key == Key.F)
				flat = !flat;
			if (e.Key == Key.Q)
				downhills = !downhills;
			if (e.Key == Key.E)
				movesun = !movesun;
			if (e.Key == Key.P)
				paused = !paused;
		}

		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);
			GL.Viewport(0, 0, Width, Height);
		}
	}
}
