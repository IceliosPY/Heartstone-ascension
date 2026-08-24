using System.Runtime.CompilerServices;

// Les tests EditMode doivent pouvoir atteindre les types internes du moteur
// sans nous obliger a rendre publique une API plus large que necessaire.
[assembly: InternalsVisibleTo("CoH.Tests.EditMode")]
