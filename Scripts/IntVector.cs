namespace TetraGen
{
    [System.Serializable]
    public struct IntVector
    {
        public int x, y, z;

        public IntVector(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
}
