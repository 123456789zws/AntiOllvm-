# AntiOllvm

![Platform](https://img.shields.io/badge/platform-Arm64-red.svg)
![Language](https://img.shields.io/badge/language-C%23-orange.svg)

## Introduction



**AntiOllvm** is an Arm64-based simulated execution framework designed to remove OLLVM's flattening obfuscation. By identifying specific patterns, it can reconstruct the complete set of if-else branches, facilitating reverse engineering and analysis.

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [How To Customize Your Analyzer](#how-to-customize-your-analyzer)
- [How to Use](#how-to-use)
    - [1. Get the CFG Info from the IDA Python Script](#1-get-the-cfg-info-from-the-ida-python-script)
    - [2. Run AntiOllvm](#2-run-antiollvm)
    - [3. Run `gen_machine_code.py`](#3-run-gen_machine_codepy)
    - [4. Rebuild CFG in IDA](#4-rebuild-cfg-in-ida)
- [How To Build](#how-to-build)
- [Additional Resources](#additional-resources)

## Features

- **Arm64 Support**: Optimized for Arm64 architectures.
- **Obfuscation Removal**: Specifically targets and removes OLLVM's flattening obfuscation.
- **CFG Reconstruction**: Rebuilds comprehensive control flow graphs with complete if-else branches.
- **IDA Integration**: Seamlessly works with IDA for analysis and rebuilding CFGs.
- **Extensible**: Easily customizable analyzer for various use cases.

## How To Customize Your Analyzer

*Coming Soon...*

## How to Use

### 1. Get the CFG Info from the IDA Python Script

Follow these steps to extract the Control Flow Graph (CFG) information using the provided IDA Python script.

```python
# Edit ida_get_cfg.py

def main():
    # Choose your function address
    func_addr = 0x181c6c  # Replace with your function address
    
    # Edit your output file path
    output_file = "C:/Users/PC5000/PycharmProjects/py_ida/cfg_output_" + hex(func_addr) + ".json"

    # Run the script
    # 1. Open IDA
    # 2. Navigate to File -> Script file -> Choose ida_get_cfg.py
    # 3. Check the output file for the CFG information

```
### 2. Run AntiOllvm
Execute the AntiOllvm tool with the CFG output.
```shell
./AntiOllvm.exe -s cfg_output_xxxx.json
```

### 3. Run gen_machine_code.py
Generate the machine code using the provided Python script. This script relies on the Keystone Engine, so ensure it's installed.
####  1. Install Keystone Engine
```shell
pip install keystone-engine
```
#### 2. Edit gen_machine_code.py:
```python
json_file_path = "fix.json"  # Replace with your fix.json path
```
#### 3. Run the script:
```shell
python gen_machine_code.py
```
### 4. Rebuild CFG in IDA
Reconstruct the CFG within IDA using the generated machine code.

```python
# Run the script

# Steps:
1. Open IDA
2. Navigate to File -> Script file -> Choose ida_rebuild_cfg.py
3. Select the output `fix.json` file from `gen_machine_code.py`
4. Enjoy the reconstructed CFG!
```
## How To Build
Clone the repository and build the project using your preferred IDE.

```bash
git clone https://github.com/IIIImmmyyy/AntiOllvm.git
```
1. Open the project in Rider or Visual Studio.
2. Build the project.

## Additional Resources
- If you are a Chinese user, you can learn more from the Kanxue Forum. [[原创] 自写简易Arm64模拟执行去除控制流平坦化
  ](https://bbs.kanxue.com/thread-284890.htm)
