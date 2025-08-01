using UnityEngine;
using System.Collections;

public class Diamond : MonoBehaviour 
{
	public float R; // Коэффициент скалистости
	public int GRAIN=8; // Коэффициент зернистости
	public bool FLAT = false; // Делать ли равнины
	public Material material; 

	private int width=2048;
	private int height=2048;
	private float WH;
	private Color32[] cols;
	private Texture2D texture;

	
	void Start () 
	{
		int resolution = width;
		WH = (float)width+height;

		// Задаём карту высот
		Terrain terrain = FindObjectOfType<Terrain> ();
		float[,] heights = new float[resolution,resolution]; 

		// Создаём карту высот
		texture = new Texture2D(width, height);
		cols = new Color32[width*height];
		drawPlasma(width, height);
		texture.SetPixels32(cols);
		texture.Apply();

		// Используем шейдер (смотри пункт 3 во 2 части)
		material.SetTexture ("_HeightTex", texture);

		// Задаём высоту вершинам по карте высот
		for (int i=0; i<resolution; i++) {
			for (int k=0;k<resolution; k++){
				heights[i,k] = texture.GetPixel(i,k).grayscale*R;
			}
		}

		// Применяем изменения
		terrain.terrainData.size = new Vector3(width, width, height);
		terrain.terrainData.heightmapResolution = resolution;
		terrain.terrainData.SetHeights(0, 0, heights);
	}

        // Считаем рандомный коэффициент смещения для высоты
	float displace(float num)
	{
		float max = num / WH * GRAIN;
		return Random.Range(-0.5f, 0.5f)* max;
	}
	
        // Вызов функции отрисовки с параметрами
	void drawPlasma(float w, float h) 
	{
		float c1, c2, c3, c4;
		
		c1 = Random.value;
		c2 = Random.value;
		c3 = Random.value;
		c4 = Random.value;
		
		divide(0.0f, 0.0f, w , h , c1, c2, c3, c4);
	}
	
        // Сама рекурсивная функция отрисовки
	void divide(float x, float y, float w, float h, float c1, float c2, float c3, float c4)
	{
		
		float newWidth = w * 0.5f;
		float newHeight = h * 0.5f;
		
		if (w < 1.0f && h < 1.0f)
		{
			float c = (c1 + c2 + c3 + c4) * 0.25f;
			cols[(int)x+(int)y*width] = new Color(c, c, c);
		}
		else
		{
			float middle =(c1 + c2 + c3 + c4) * 0.25f + displace(newWidth + newHeight);
			float edge1 = (c1 + c2) * 0.5f;
			float edge2 = (c2 + c3) * 0.5f;
			float edge3 = (c3 + c4) * 0.5f;
			float edge4 = (c4 + c1) * 0.5f;

			if(!FLAT){
				if (middle <= 0)
				{
					middle = 0;
				}
			else if (middle > 1.0f)
				{
					middle = 1.0f;
				}
			}
			divide(x, y, newWidth, newHeight, c1, edge1, middle, edge4);
			divide(x + newWidth, y, newWidth, newHeight, edge1, c2, edge2, middle);
			divide(x + newWidth, y + newHeight, newWidth, newHeight, middle, edge2, c3, edge3);
			divide(x, y + newHeight, newWidth, newHeight, edge4, middle, edge3, c4);
		}
	}
}
