/* Make the following tests

- test that compiles a simple noise function, samples over a batch of points, and makes sure they aren't all the same value

- test that compiles the same noise function twice w/ different seeds
- validates that the 2 noise function have different values at some points

- test that takes a noise function added to itself (same instance) and validates that its equivelent to that noise function added to itself

- test that takes a noise function added to a different instance of the exact same noise function, and validates that the noise function is not equal to 2x either of the base noise functions (showing that they got diff seeds)

- test that takes a FBM and validates that the frequency and accumulate operations get folded in correctly such that the compiled output. should also validate equivelence to a hardcoded expected output.

- For tests checking equivelence, please allow for epsilon fp error
- Please document the tests w/ comments
*/
