using System;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;
using System.Collections.Generic;

namespace Enviro3D {
	
	public class TerrainBlock
	{
		enum Diagonal {tlbr, trbl} ;
		public TerrainTriangle top;
		public TerrainTriangle bottom;
		Vector3 bl_helper;
		Vector3 br_helper;
		Vector3 tl_helper;
		Vector3 tr_helper;
		Diagonal type;
		int I;
		int J;

		public TerrainBlock(float porosity, float tl, float tr, float bl, float br, int i, int j, float scale) {
			type = (Math.Abs(tl - tr) < Math.Abs(tr - bl)) ? Diagonal.tlbr : Diagonal.trbl ;

			this.I = i;
			this.J = j;

			if (type == Diagonal.tlbr) {
				top = new TerrainTriangle(tr, tl, br, Triangle.tr, I, J, scale);
				bottom = new TerrainTriangle(tl, bl, br, Triangle.bl, I, J, scale);

				bl_helper = bottom.normal;
				br_helper = bottom.normal + top.normal;
				tl_helper = br_helper;
				tr_helper = top.normal;
			}
			else {
				top = new TerrainTriangle(tr, tl, bl, Triangle.tl , I, J, scale);
				bottom = new TerrainTriangle(tr, bl, br, Triangle.br ,I, J, scale);

				br_helper = bottom.normal;
				bl_helper = bottom.normal + top.normal;
				tr_helper = bl_helper;
				tl_helper = top.normal;
			}
		}

		public TerrainTriangle Left()
		{
			if (type == Diagonal.tlbr)
				return bottom;
			else
				return top;
		}

		public TerrainTriangle Right()
		{
			if (type == Diagonal.tlbr)
				return top;
			else
				return bottom;
		}

		public void Flow(int turn)
		{
			top.Flow(turn);
			bottom.Flow(turn);
		} 

		public void DiscoverNeighbours(TerrainBlock[,] blocks)
		{
			bool not_above = J - 1 < 0;
			bool not_below = J + 1 >= blocks.GetLength(0);
			bool not_left = I - 1 < 0;
			bool not_right = I + 1 >= blocks.GetLength(1);

			if (type == Diagonal.tlbr)
			{
				top.neighbours[0] = not_above ? null : blocks[I, J - 1].bottom;
				top.neighbours[1] = bottom;
				top.neighbours[2] = not_right ? null : blocks[I + 1, J].Left();

				bottom.neighbours[0] = not_left ? null : blocks[I - 1, J].Right();
				bottom.neighbours[1] = not_below? null : blocks[I, J + 1].top;
				bottom.neighbours[2] = top;
			}
			else
			{
				top.neighbours[0] = not_above ? null : blocks[I, J - 1].bottom;
				top.neighbours[1] = not_left ? null : blocks[I - 1, J].Right();
				top.neighbours[2] = bottom;

				bottom.neighbours[0] = top;
				bottom.neighbours[1] = not_below ? null : blocks[I, J + 1].top ;
				bottom.neighbours[2] = not_right ? null : blocks[I + 1, J].Left();
			}
		}

		public void CalcNormals(TerrainBlock[,] blocks)
		{
			bool not_above = J - 1 < 0;
			bool not_below = J + 1 >= blocks.GetLength(0);
			bool not_left = I - 1 < 0;
			bool not_right = I + 1 >= blocks.GetLength(1);

			Vector3 top_r = not_above ? Vector3.Zero : blocks[I, J - 1].br_helper ;
			Vector3 top_l = not_above ? Vector3.Zero : blocks[I, J - 1].bl_helper ;
			Vector3 right_t = not_right ? Vector3.Zero : blocks[I + 1, J].tl_helper ;
			Vector3 right_b = not_right ? Vector3.Zero : blocks[I + 1, J].bl_helper ;

			Vector3 bot_r = not_below ? Vector3.Zero : blocks[I, J + 1].tr_helper ;
			Vector3 bot_l = not_below ? Vector3.Zero : blocks[I, J + 1].tl_helper ;
			Vector3 left_t = not_left ? Vector3.Zero : blocks[I - 1, J].tr_helper ;
			Vector3 left_b = not_left ? Vector3.Zero : blocks[I - 1, J].br_helper ;

			Vector3 tl = not_above || not_left ? Vector3.Zero : blocks[I -1, J-1].br_helper ;
			Vector3 tr = not_above || not_right ? Vector3.Zero : blocks[I +1, J-1].bl_helper ;
			Vector3 bl = not_below || not_left ? Vector3.Zero : blocks[I -1, J+1].tr_helper ;
			Vector3 br = not_below || not_right ? Vector3.Zero : blocks[I + 1, J+1].tl_helper ;

			if (type == Diagonal.tlbr) {
				//top triangle
				Vector3 ttr = top.normal + top_r + tr + right_t ;
				Vector3 ttl = top.normal + bottom.normal + top_l + tl + left_t ;
				Vector3 tbr = top.normal + bottom.normal + bot_r + br + right_b ;

				top.CalcNormals(ttr, ttl, tbr);

				//bottom triangle
				Vector3 btl = ttl;
				Vector3 bbl = bottom.normal + bot_l + bl + left_b ;
				Vector3 bbr = tbr;

				bottom.CalcNormals(btl, bbl, bbr);
			}
			else {
				//bottom triangle
				Vector3 btr = top.normal + bottom.normal + top_r + tr + right_t ;
				Vector3 bbl = top.normal + bottom.normal + bot_l + bl + left_b ;
				Vector3 bbr = bottom.normal + bot_r + br + right_b ;

				bottom.CalcNormals(btr, bbl, bbr);

				//top triangle
				Vector3 ttr = btr;
				Vector3 ttl = top.normal + top_l + tl + left_t ;
				Vector3 tbl = bbl;

				top.CalcNormals(ttr,ttl, tbl);
			}
		}


		public void Draw(bool flat, bool downhills)
		{
			top.Draw(flat, downhills);
			bottom.Draw(flat, downhills);
		}
	}
}

