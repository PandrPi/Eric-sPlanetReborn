namespace Planet.Chunks
{
	/// <summary>
	/// Interface that declares methods for quad tree nodes
	/// </summary>
	public interface IQuadTreeNode
	{
		/// <summary>
		/// 
		/// </summary>
		void Initialize();

		/// <summary>
		/// Returns the parent node of the current node or null the current node has no parent 
		/// </summary>
		IQuadTreeNode GetParent();

		/// <summary>
		/// Returns an array of the children nodes
		/// </summary>
		IQuadTreeNode[] GetChildren();

		/// <summary>
		/// Divides the current node into four smaller child nodes and initializes them
		/// </summary>
		void Divide();
	}
}
