using System;
using System.Runtime;
using fazz.Models.Entities;
namespace fazz.Models.Requests
{
	public class AddOrUpdateCategoryRequest
	{
		public int Id { get; set; }

		public string? Title { get; set; }

        public string? Description { get; set; }

		public int Credit { get; set; }
		public List<Question> Questions { get; set; }
		
		
    }
}

