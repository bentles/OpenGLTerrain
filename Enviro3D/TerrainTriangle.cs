using System;
using System.Drawing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;
using System.Collections.Generic;

namespace Enviro3D {
	
	public class TerrainTriangle
	{
		//variables for dealing with water flow
		public float low, high, porosity, flow;
		public float ground_capacity;
		public float surface_water, ground_water;
		public float new_surface_water, new_ground_water;

		int turn = 0;  
		int lastwater = 0;

		float wetness = 1;

		public Vector3[] points;
		public Vector3[] normals;
		public Vector3 normal;
		public Vector3 downhill;

		//triangle_side_01, triangle_side_12, triangle_side_20;
		public TerrainTriangle[] neighbours = new TerrainTriangle[3];
		//normals for each direction
		//side_01, side_12, side_20;
		public Vector3[] neighbour_normals = new Vector3[3];

		//where the triangle is
		public Triangle t;

		//start at the highest, rightmost point. work anticlockwise. highest has priority
		public TerrainTriangle(float a, float b, float c, Triangle t, int I, int J, float scale)
		{
			this.t = t;
			surface_water = 0.3f;
			new_surface_water = surface_water;
			ground_water = 0;
			new_ground_water = ground_water;

			points = new Vector3[3];
			normals = new Vector3[3];

			float tlX = I * scale;
			float tlY = J * scale;

			if (t == Triangle.bl) {
				points[0] = new Vector3(tlX  , a, tlY);
				points[1] = new Vector3(tlX, b, tlY + scale);
				points[2] = new Vector3(tlX + scale, c, tlY + scale);

			}
			else if (t == Triangle.br) {
				points[0] = new Vector3(tlX + scale, a, tlY);
				points[1] = new Vector3(tlX, b, tlY + scale);
				points[2] = new Vector3(tlX + scale, c, tlY + scale);

			}
			else if (t == Triangle.tr) {
				points[0] = new Vector3(tlX + scale, a, tlY);
				points[1] = new Vector3(tlX, b, tlY);
				points[2] = new Vector3(tlX + scale, c, tlY + scale);

			}						   
			else {					  
				points[0] = new Vector3(tlX + scale, a, tlY);
				points[1] = new Vector3(tlX, b, tlY);
				points[2] = new Vector3(tlX, c, tlY + scale);
			}

			//find the low point of the triangle
			low = points[0].Y;
			low = (low > points[1].Y) ? points[1].Y : low;
			low = (low > points[2].Y) ? points[2].Y : low;

			//find the high point of the triangle
			high = points[0].Y;
			high = (high < points[1].Y) ? points[1].Y : high;
			high = (high < points[2].Y) ? points[2].Y : high;

			normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).Normalized();
			downhill = Vector3.Cross(Vector3.Cross(normal, -Vector3.UnitY), normal).Normalized();

			if (Vector3.Dot(downhill, -Vector3.UnitY) < 0)
				downhill = -downhill;

			float slope = -downhill.Y / downhill.X;
			float inv_slope = 1 / (slope);

			//find side normals                
			neighbour_normals[0] = Vector3.Cross(points[1] - points[0], normal).Normalized();
			neighbour_normals[1] = Vector3.Cross(points[2] - points[1], normal).Normalized();
			neighbour_normals[2] = Vector3.Cross(points[0] - points[2], normal).Normalized();

			//triangles to each side
			neighbours[0] = null;
			neighbours[1] = null;
			neighbours[2] = null;

			//modified sigmoid fn to constrain porosity to sane amounts for small slopes
			porosity = 1 / (1 + (float)Math.Exp(-inv_slope + 6));
			ground_capacity = porosity;
			flow = 1 / (1 + (float)Math.Exp(-slope + 6));
		}

		public void TryUpdate(int cur_turn)
		{
			if (turn != cur_turn)
			{
				turn = cur_turn;
				surface_water = new_surface_water;
				ground_water = new_ground_water;

				if (surface_water < 0 )
				{
					lastwater++;
					wetness = 1 - (1 / (1 + (float)Math.Exp((lastwater/100) - 6)));
				}
			}                
		}

		public bool IsInert()
		{
			return surface_water <= 0;
		}

		public float Height()
		{
			return low + surface_water;
		}

		public void Flow(int cur_turn)
		{
			TryUpdate(cur_turn);

			if (!IsInert())
			{
				float seep_speed = 0.01f;

				//send some water into the ground
				float seep = (ground_capacity - ground_water) > porosity * seep_speed ? porosity * seep_speed : (ground_capacity - ground_water);
				new_surface_water -= seep;
				new_ground_water += seep;

				float total = 0;
				foreach (var neigh in neighbour_normals)
				{
					float a = Vector3.Dot(downhill, neigh);
					total = a < 0 ? total : total + a;
				}


				//send some water into neighbours
				for (int i = 0; i < neighbours.Length; i++)
				{   
					if (neighbours[i] != null)
					{
						float a = Vector3.Dot(downhill, neighbour_normals[i]);
						a = a < 0 ? 0 : a;
						float direction_modifier = a / total;

						//water above high mark
						float highwater = low + surface_water - high;

						float diff = Height() - neighbours[i].Height();
						if (diff > highwater / 3f)
							diff = highwater / 3f;
						if (diff < -highwater / 3f)
							diff = -highwater / 3f;

						//water below high mark
						float flow_speed = 0.65f;
						//multiply by direction modifier
						float lowdiff = flow_speed * direction_modifier;

						//actual updates
						neighbours[i].TryUpdate(cur_turn);

						//water below high mark
						if (high > neighbours[i].Height())
						{
							neighbours[i].new_surface_water += lowdiff;
							new_surface_water -= lowdiff;
						}  

						neighbours[i].new_surface_water += diff * flow_speed / 3f;
						new_surface_water -= diff * flow_speed / 3f;
					}
				}
			}                
		}

		//start at the highest, rightmost point. work anticlockwise. highest has priority
		public void CalcNormals(Vector3 a, Vector3 b, Vector3 c)
		{
			normals[0] = a.Normalized();
			normals[1] = b.Normalized();
			normals[2] = c.Normalized();
		}

		public void Draw(bool flat, bool downhills)
		{
			//ground
			GL.Begin(PrimitiveType.Triangles);
			{
				float greendex = ground_water / ground_capacity;

				//Console.WriteLine(wetness);
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 10 + 80 * wetness );
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 1f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4((greendex*-0.8f) + 0.8f   , (greendex* - 0.35f) + 0.8f  , (greendex * -0.5f) + 0.5f  , 1f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(0.3f   , 0.3f  , 0.3f  , 1f));
				//GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 10);

				GL.Normal3(normal);
				for (int i = 0; i < points.Length; i++) {
					if (!flat)
						GL.Normal3(normals[i]);
					GL.Vertex3(points[i]);
				}
			}
			GL.End();

			if (surface_water > 0)
			{
				//water
				GL.Begin(PrimitiveType.Triangles);
				{
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 500);
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.2f  , 0.8f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(1f   , 1f  , 1f  , 1f));
					for (int i = 0; i < points.Length; i++) {
						if (!flat)
							GL.Normal3(normals[i]);
						if (Height() < high)
							GL.Vertex3(points[i] + new Vector3(0, surface_water, 0));
						else {
							GL.Normal3(Vector3.UnitY);
							GL.Vertex3(new Vector3(points[i].X, low + surface_water, points[i].Z));
						}
					}
				}
				GL.End();
			}

			//downhill vectors
			if (downhills)
			{
				Vector3 st = Vector3.Lerp(Vector3.Lerp(points[0], points[1], 0.5f), points[2], 0.3333f);
				GL.Begin(PrimitiveType.Lines);
				{
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.3f, 0.3f, 1f, 0.3f));
					GL.Vertex3(st);
					GL.Vertex3(st + downhill);
				}
				GL.End();

				GL.PointSize(3f);
				GL.Begin(PrimitiveType.Points);
				{
					GL.Vertex3(st + downhill);
				}
				GL.End();	
			}
		}
	}
}

