using System;
using System.Drawing;
using SimplexNoise;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;
using System.Collections.Generic;

namespace Enviro3D
{
	public enum Triangle {tl, tr, bl, br}

	public class Terrain
	{
		public bool draw_downhill = false;
		public bool flat = false;

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

			//create terrain
			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {					
					foreach (var item in noise_gen) {
						if (item.add)
							terrain_vertices[i,j] += item.getNoise(i,j);
						else
							terrain_vertices[i,j] *= item.getNoise(i,j);
					}	

					terrain_vertices[i,j] *= scale;

					if (i > 0 && j > 0) {
						//make and store info about terrain from vertices
						terrain_blocks[i-1,j-1] = new TerrainBlock(5, 
							terrain_vertices[i-1, j-1], terrain_vertices[i, j-1], 
							terrain_vertices[i-1, j], terrain_vertices[i, j], 
							i-1, j-1, scale);
					}
				}
			}

			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {
					if (i > 0 && j > 0) {
                        //compute normals for gouraud shading
						terrain_blocks[i - 1, j - 1].CalcNormals(terrain_blocks);
                        //discover neighbours for all terrain triangles
                        terrain_blocks[i - 1, j - 1].DiscoverNeighbours(terrain_blocks);
					}
				}
			}

		}

		public void Draw(bool flat, bool downhills) {
			int width = terrain_vertices.GetLength(0) - 1;
			int height = terrain_vertices.GetLength(1) - 1;

			float wall_height = 30 * scale;


			//draw terrain
			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {
					terrain_blocks[i,j].Draw(flat, downhills);
				}
			}
				
			//draw walls
			Vector3 blb = new Vector3(0,-wall_height,0);
			Vector3 brb = new Vector3(width * scale,-wall_height,0);
			Vector3 brf = new Vector3(width * scale,-wall_height, width * scale);
			Vector3 blf = new Vector3(0,-wall_height, width * scale);		

			//draw front wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				
				for (int i = width; i >= 0; i--) {
					GL.Normal3(new Vector3(0,0,1));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.15f, 0.15f, 0.15f, 0.3f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.18f   , 0.18f  , 0.18f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(1f   , 1f  , 1f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess,  100f);

					GL.Vertex3(blf + (brf - blf)*i/(float)width);
					GL.Vertex3(i*scale, terrain_vertices[i,width], width*scale);
				}
			}
			GL.End();

			//draw right wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				for (int i = 0; i <=  width; i++) {					
					GL.Normal3(new Vector3(1,0,0));			
					GL.Vertex3(brb + (brf - brb) * i/(float)width);
					GL.Vertex3(width * scale, terrain_vertices[width, i], i * scale);
				}
			}
			GL.End();
			
			//draw left wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				for (int i = width; i >= 0; i--) {
					GL.Normal3(new Vector3(-1,0,0));
					GL.Vertex3(blb + (blf - blb) * i/(float)width);
					GL.Vertex3(0, terrain_vertices[0, i], i * scale);
				}
			}
			GL.End();
			
			//draw back wall
			GL.Begin(PrimitiveType.QuadStrip);
			{				

				for (int i = 0; i <= width; i++) {
					GL.Normal3(new Vector3(0,0,-1));
					GL.Vertex3(blb + (brb - blb) * i/(float)width);
					GL.Vertex3(i*scale, terrain_vertices[i, 0], 0);
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

        public void UpdateState(int tick)
        {
            int width = terrain_vertices.GetLength(0) - 1;
            int height = terrain_vertices.GetLength(1) - 1;

            //draw terrain
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    //make water flow
                    terrain_blocks[i, j].Flow(tick);
                }
            }

        }
	}
}

