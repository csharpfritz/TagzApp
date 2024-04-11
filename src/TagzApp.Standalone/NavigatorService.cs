using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagzApp.Standalone;

public class NavigatorService
{

	public NavigationManager NavigationManager { get; set; }

	public void NavigateTo(string url)
	{

		NavigationManager.NavigateTo(url);

	}

}
