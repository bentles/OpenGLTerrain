using System;
using System.Drawing;
using SimplexNoise;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;
using System.Collections.Generic;

namespace Enviro3D
{
	public class Terrain
	{
		public enum Triangle {tl, tr, bl, br}

		public struct TerrainBlock
		{
			enum Diagonal {tlbr, trbl} ;

			TerrainTriangle top;
			TerrainTriangle bottom;
			Diagonal type;

			public TerrainBlock(float porosity, float tl, float tr, float bl, float br, Vector2 bot_left, float scale) {
				type = (Math.Abs(tl - tr) < Math.Abs(tr - bl)) ? Diagonal.tlbr : Diagonal.trbl ;

				if (type == Diagonal.tlbr) {
					top = new TerrainTriangle(tr, br, tl, Triangle.tr, bot_left, scale);
					bottom = new TerrainTriangle(tl, br, bl, Triangle.bl, bot_left, scale);
				}
				else {
					top = new TerrainTriangle(tr, br, bl, Triangle.br ,bot_left, scale);
					bottom = new TerrainTriangle(tr, bl, tl, Triangle.tl ,bot_left, scale);
				}
			}

			public void Draw()
			{
				top.Draw();
				bottom.Draw();

			}
		}

		public struct TerrainTriangle
		{
			public float surface_water;
			float ground_water;
			public float porosity;
			Vector3[] points;
			Vector3 normal;
			Vector3 downhill;

			//start at the highest, rightmost point. work anticlockwise. highest has priority
			public TerrainTriangle(float a, float b, float c, Triangle t, Vector2 bl, float scale)
			{
				surface_water = 0;
				ground_water = 0;
				porosity = 0;
				points = new Vector3[3];
				if (t == Triangle.bl) {
					points[0] = new Vector3(bl.X , a, bl.Y + scale);
					points[1] = new Vector3(bl.X + scale, b, bl.Y);
					points[2] = new Vector3(bl.X, c, bl.Y);

				}
				else if (t == Triangle.br) {
					points[0] = new Vector3(bl.X + scale, a, bl.Y + scale);
					points[1] = new Vector3(bl.X + scale, b, bl.Y);
					points[2] = new Vector3(bl.X, c, bl.Y);

				}
				else if (t == Triangle.tr) {
					points[0] = new Vector3(bl.X + scale, a, bl.Y + scale);
					points[1] = new Vector3(bl.X + scale, b, bl.Y);
					points[2] = new Vector3(bl.X, c, bl.Y + scale);

				}
				else {
					points[0] = new Vector3(bl.X + scale, a, bl.Y + scale);
					points[1] = new Vector3(bl.X, b, bl.Y);
					points[2] = new Vector3(bl.X, c, bl.Y + scale);
				}

				normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).Normalized();
				downhill = Vector3.Cross(Vector3.Cross(normal, -Vector3.UnitY), normal).Normalized();

				if (Vector3.Dot(downhill, -Vector3.UnitZ) < 0)
					downhill = -downhill;
			}

			public void Draw()
			{
				GL.Begin(PrimitiveType.Triangles);
				{
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 0.3f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.7f  , 0.2f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(1f   , 1f  , 1f  , 1f));
					GL.Material(MaterialFace.Front, MaterialParameter.Shininess, 50f);

					GL.Normal3(normal);
					for (int i = 0; i < points.Length; i++) {
						GL.Vertex3(points[i]);
					}
				}
				GL.End();
			}
		}
			

		TerrainBlock[,] terrain_blocks;
		List<NoiseWrapper> noise_gen;
		float[,] terrain_vertices;
		float scale;
		public Terrain (int width, int height, float scale, List<NoiseWrapper> noisy)
		{
			terrain_vertices = new float[width + 1,height + 1];
			terrain_blocks = new TerrainBlock[width, height];
			this.scale = scale;
			noise_gen = noisy;

		}

		public void GenerateTerrainFromNoise()
		{
			int width = terrain_vertices.GetLength(0);
			int height = terrain_vertices.GetLength(1);	

	

			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {					
					foreach (var item in noise_gen) {
						if (item.add)
							terrain_vertices[i,j] += item.getNoise(i,j);
						else
							terrain_vertices[i,j] *= item.getNoise(i,j);
					}				

					if (i > 0 && j > 0) {
						//make and store info about terrain from vertices
						terrain_blocks[i-1,j-1] = new TerrainBlock(5, 
							terrain_vertices[i-1, j-1], terrain_vertices[i, j-1], 
							terrain_vertices[i-1, j], terrain_vertices[i, j], 
							new Vector2((i - 1)*scale, -(j)*scale), scale);
					}
				}
			}
		}

		public void Draw() {
			int width = terrain_vertices.GetLength(0) - 1;
			int height = terrain_vertices.GetLength(1) - 1;

			int wall_height = 30;


			//GL.ShadeModel(ShadingModel.Smooth);
			//draw terrain
			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {
					terrain_blocks[i,j].Draw();
				}
			}

			//draw walls
			Vector3 blf = new Vector3(0,-wall_height,0);
			Vector3 brf = new Vector3(width * scale,-wall_height,0);
			Vector3 brb = new Vector3(width * scale,-wall_height, -width * scale);
			Vector3 blb = new Vector3(0,-wall_height,-width * scale);

			//draw front wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				GL.Normal3(0,0,1);
				for (int i = width; i >= 0; i--) {
					GL.Vertex3(blf + (brf - blf)*i/(float)width);
 					GL.Vertex3(i*scale, terrain_vertices[i,0], 0);
				}
			}
			GL.End();

			//draw right wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				GL.Normal3(1,0,0);			
				for (int i = width; i >= 0; i--) {					
					GL.Vertex3(brf + (brb - brf) * i/(float)width);
					GL.Vertex3(width * scale, terrain_vertices[width, i], - i * scale);
				}
			}
			GL.End();
			
			//draw left wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				GL.Normal3(-1,0,0);		
				for (int i = 0; i <= width; i++) {
					GL.Vertex3(blf + (blb - blf) * i/(float)width);
					GL.Vertex3(0, terrain_vertices[0, i], -i * scale);
				}
			}
			GL.End();
			
			//draw back wall
			GL.Begin(PrimitiveType.QuadStrip);
			{				
				GL.Normal3(0,0,-1);

				for (int i = 0; i <= width; i++) {
					GL.Vertex3(blb + (brb - blb) * i/(float)width);
					GL.Vertex3(i*scale, terrain_vertices[i, width], -width * scale);
				}
			}
			GL.End();

			//draw bottom
			GL.Begin(PrimitiveType.Quads);
			{
				GL.Normal3(0,-1,0);
				GL.Vertex3(brf);
				GL.Vertex3(blf);
				GL.Vertex3(blb);
				GL.Vertex3(brb);
			}
			GL.End();
		}
	}
}

