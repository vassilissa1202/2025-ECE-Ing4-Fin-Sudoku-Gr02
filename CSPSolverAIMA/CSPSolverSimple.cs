using System;
using System.Collections.Generic;
using System.Linq;
using Sudoku.Shared;  // Assurez-vous que SudokuGrid et ISudokuSolver se trouvent dans ce namespace

namespace Sudoku.CSPAimaSolver
{
    // Exemple d'interface ISudokuSolver (à adapter selon votre définition)
    // public interface ISudokuSolver
    // {
    //     SudokuGrid Solve(SudokuGrid grid);
    // }
    
    public class CSPSolverSimple : ISudokuSolver
    {
        /// <summary>
        /// Calcule, pour chaque cellule (i,j) du Sudoku, l’ensemble des cellules voisines
        /// (même ligne, même colonne et même bloc 3x3).
        /// </summary>
        /// <returns>Un dictionnaire où la clé est une cellule (tuple (i,j)) et la valeur l’ensemble de ses voisins.</returns>
        private Dictionary<(int row, int col), HashSet<(int row, int col)>> ComputeNeighbors()
        {
            var neighbors = new Dictionary<(int, int), HashSet<(int, int)>>();
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    var cell = (i, j);
                    var nbs = new HashSet<(int, int)>();

                    // Voisins sur la même ligne
                    for (int col = 0; col < 9; col++)
                    {
                        if (col != j)
                            nbs.Add((i, col));
                    }

                    // Voisins sur la même colonne
                    for (int row = 0; row < 9; row++)
                    {
                        if (row != i)
                            nbs.Add((row, j));
                    }

                    // Voisins dans le même bloc 3x3
                    int blockRow = (i / 3) * 3;
                    int blockCol = (j / 3) * 3;
                    for (int row = blockRow; row < blockRow + 3; row++)
                    {
                        for (int col = blockCol; col < blockCol + 3; col++)
                        {
                            if (row == i && col == j)
                                continue;
                            nbs.Add((row, col));
                        }
                    }

                    neighbors[cell] = nbs;
                }
            }
            return neighbors;
        }

        /// <summary>
        /// Convertit la grille initiale en un dictionnaire des domaines pour chaque cellule.
        /// Si une cellule est vide (valeur 0), son domaine est {1,2,...,9} ; sinon, son domaine est un singleton.
        /// </summary>
        private Dictionary<(int, int), HashSet<int>> ParseGrid(int[,] grid)
        {
            var domains = new Dictionary<(int, int), HashSet<int>>();
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    var cell = (i, j);
                    int value = grid[i, j];
                    if (value == 0)
                    {
                        domains[cell] = new HashSet<int>(Enumerable.Range(1, 9));
                    }
                    else
                    {
                        domains[cell] = new HashSet<int> { value };
                    }
                }
            }
            return domains;
        }

        /// <summary>
        /// Sélectionne une variable non assignée (domaine de taille > 1) en appliquant l’heuristique MRV.
        /// Retourne la cellule ayant le domaine le plus restreint.
        /// </summary>
        private (int, int)? SelectUnassignedVariable(Dictionary<(int, int), HashSet<int>> domains)
        {
            (int, int)? best = null;
            int bestSize = int.MaxValue;
            foreach (var kvp in domains)
            {
                if (kvp.Value.Count > 1 && kvp.Value.Count < bestSize)
                {
                    best = kvp.Key;
                    bestSize = kvp.Value.Count;
                }
            }
            return best;
        }

        /// <summary>
        /// Effectue le forward checking : on fixe la valeur 'value' pour la variable 'var' et on
        /// retire cette valeur des domaines des voisins. Si un domaine devient vide, retourne null.
        /// </summary>
        private Dictionary<(int, int), HashSet<int>> ForwardCheck(
            Dictionary<(int, int), HashSet<int>> domains,
            (int, int) var,
            int value,
            Dictionary<(int, int), HashSet<(int, int)>> neighbors)
        {
            // Création d'une copie profonde des domaines
            var newDomains = domains.ToDictionary(
                kvp => kvp.Key,
                kvp => new HashSet<int>(kvp.Value));

            // Affecter la valeur à la variable (domaine singleton)
            newDomains[var] = new HashSet<int> { value };

            // Pour chaque voisin, retirer la valeur affectée
            foreach (var nb in neighbors[var])
            {
                if (newDomains[nb].Contains(value))
                {
                    newDomains[nb].Remove(value);
                    if (newDomains[nb].Count == 0)
                        return null;
                }
            }
            return newDomains;
        }

        /// <summary>
        /// Algorithme récursif de backtracking.
        /// Si toutes les variables ont un domaine singleton, la solution est trouvée.
        /// Sinon, sélectionne une variable non assignée et tente chaque valeur possible avec forward checking.
        /// </summary>
        private Dictionary<(int, int), HashSet<int>> Backtrack(
            Dictionary<(int, int), HashSet<int>> domains,
            Dictionary<(int, int), HashSet<(int, int)>> neighbors)
        {
            // Si toutes les cellules ont un domaine singleton, la solution est complète
            if (domains.All(kvp => kvp.Value.Count == 1))
                return domains;

            var var = SelectUnassignedVariable(domains);
            if (var == null)
                return null; // Théoriquement, cela ne devrait pas se produire

            foreach (int val in domains[var.Value])
            {
                var newDomains = ForwardCheck(domains, var.Value, val, neighbors);
                if (newDomains != null)
                {
                    var result = Backtrack(newDomains, neighbors);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Méthode publique de résolution du Sudoku.
        /// Elle prend un SudokuGrid (contenant un tableau 9x9 d'entiers dans la propriété Cells)
        /// et retourne un nouveau SudokuGrid résolu.
        /// </summary>
        public SudokuGrid Solve(SudokuGrid grid)
        {
            // On suppose que grid.Cells est un tableau 9x9 d'entiers
            int[,] initialGrid = grid.Cells;
            var domains = ParseGrid(initialGrid);
            var neighbors = ComputeNeighbors();
            var solution = Backtrack(domains, neighbors);

            if (solution == null)
                throw new Exception("Aucune solution trouvée.");

            int[,] solvedGrid = new int[9, 9];
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    var cell = (i, j);
                    solvedGrid[i, j] = solution[cell].First(); // Chaque domaine est singleton
                }
            }

            // Construire et retourner un nouveau SudokuGrid avec la grille résolue
            SudokuGrid resultGrid = new SudokuGrid();
            resultGrid.Cells = solvedGrid;
            return resultGrid;
        }
    }
}
