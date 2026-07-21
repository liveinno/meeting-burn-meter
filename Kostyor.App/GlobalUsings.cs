// Явные глобальные using'и (эквивалент ImplicitUsings для non-web SDK).
// Заданы вручную, потому что временный WPF-проект markup-компиляции (_wpftmp)
// не наследует ImplicitUsings и иначе не видит System.IO / System.Net.Http и т.п.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
