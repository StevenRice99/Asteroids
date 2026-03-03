# ML-Asteroids

Teaching an agent to player [Asteroids](https://en.wikipedia.org/wiki/Asteroids_(video_game) "Asteroids") with [Unity ML-Agents](https://docs.unity3d.com/Packages/com.unity.ml-agents@latest "Unity ML-Agents"). See a [web demo](https://stevenrice.ca/ml-asteroids "ML-Asteroids").

- [Purpose](#purpose "Purpose")
- [Game Overview](#game-overview "Game Overview")
- [Agent Design](#agent-design "Agent Design")
- [Agent Rewards](#agent-rewards "Agent Rewards")
- [Agent Training](#agent-training "Agent Training")
  - [Heuristic Agent](#heuristic-agent "Heuristic Agent")
- [Results](#results "Results")
- [Running](#running "Running")
  - [Run Training](#run-training "Run Training")
    - [Helper Functions](#helper-functions "Helper Functions")
- [Resources](#resources "Resources")

## Purpose

The purpose is this project is for use as a learning resources for [Unity ML-Agents](https://docs.unity3d.com/Packages/com.unity.ml-agents@latest "Unity ML-Agents"), highlighting how different methods can be applied to try and overcome a classic game.

## Game Overview

- The agent can travel within a set, square area.
- They can move forwards, turn left or right, and fire at asteroids.

## Agent Design

The agent's has several discrete actions:

1. Stay still = `0` and move = `1`.
2. Don't turn = `0`, turn left = `1`, and turn right = `2`.
3. Don't fire = `0` and fire = `1`.

The agent's sensing of the environment constists of, with all stacked across two frames:

1. **Agent position** - Both the agent's previous and current positions in the playable area are given along both the horizontal and vertical axes each in the range of `[0, 1]`.
2. **Agent rotation** - The agent's rotation scaled to `[0, 1]`.
3. **Raycast sensor** - A 2D raycast sensor is attached to the agent, firing 30 rays on each side of the agent.

## Agent Rewards

- A reward of `0.5` is given for every asteroid destroyed.
- A penalty of `-1` is given for being eliminated.
- A penalty of `-0.1` is given for every shot fired.

## Agent Training

The agent was trained with [Proximal Policy Optimization (PPO)](https://doi.org/10.48550/arXiv.1707.06347 "Proximal Policy Optimization Algorithms"), [training curriculum](#curriculum-learning "Curriculum Learning"), a [curiosity reward signal](https://doi.org/10.48550/arXiv.1705.05363 "Curiosity-driven Exploration by Self-supervised Prediction") to encourage exploration, and imitation learning, being both Behavioral Cloning (BC) and [Generative Adversarial Imitation Learning (GAIL)](https://doi.org/10.48550/arXiv.1606.03476 "Generative Adversarial Imitation Learning"). The [demonstrations](#demonstration-recording "Demonstration Recording") for imitation learning were recorded using the [heuristic agent](#heuristic-agent "Heuristic Agent").

### Heuristic Agent

The heuristic agent tries to aim at the "best" asteroid. This is determined by first seeing if any asteroids are on a collision path with the agent. If there are, the nearest asteroid on a collision path is chosen. Otherwise, the nearest asteroid not on a collision path is chosen. Then, the agent rotates to face the selected asteroid, firing if it is facing said asteroid. The agent does not move on its own, but human keyboard controls can take over, allowing for manual movement and firing using the arrow keys or WASD alongside space to fire.

## Results

TODO.

## Running

If you just wish to see the agent in action, you can run the [web demo](https://stevenrice.ca/ml-asteroids "ML-Asteroids").

### Run Training

To train the agent, you can either read the [Unity ML-Agents documentation](https://docs.unity3d.com/Packages/com.unity.ml-agents@latest "Unity ML-Agents") to learn how to install and run [Unity ML-Agents](https://docs.unity3d.com/Packages/com.unity.ml-agents@latest "Unity ML-Agents"), or use the provided [helper functions](#helper-functions "Helper Functions") to train the agent.

#### Helper Functions

The helper files have been made for Windows and you must [install uv](https://docs.astral.sh/uv/#installation "UV Installation"). One installed, from the top menu of the Unity editor, you can select `ML-Asteroids` followed by the desired command to run.

- `Train` - Run training.
- `TensorBoard` - This will open your browser to see the [TensorBoard](https://www.tensorflow.org/tensorboard "TensorBoard") logs of the training of all models.
- `Install` - If you have [uv](https://docs.astral.sh/uv "uv") installed for Python, this will set up your environment for running all other commands.
- `Activate` - This will open a terminal in your [uv](https://docs.astral.sh/uv "uv") Python virtual environment for this project, allowing you to run other commands.

## Resources

Assets are from [Asteroids by Zigurous](https://github.com/zigurous/unity-asteroids-tutorial "Asteroids by Zigurous").