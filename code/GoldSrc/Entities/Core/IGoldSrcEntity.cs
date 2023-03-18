namespace MapParser.GoldSrc.Entities
{
	public interface IGoldSrcEntity
	{
		public EntityParser.EntityData entData { get; set; }
		public void Delete() {}
	}
}
