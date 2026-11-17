// System.Threading.Timer и System.Windows.Forms.Timer одновременно попадают
// в область видимости из-за ImplicitUsings — фиксируем WinForms-таймер как основной.
global using Timer = System.Windows.Forms.Timer;
