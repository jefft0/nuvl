# For each file in the current directory and all subdirectories, write a line to sha-256-map.txt with
# the ni hash of the file and its relative path, for example:
# f4OxZX_x_FO5LcGBSKHWXfwtSx-j1ncoSt3SABJtkGk ./hello.txt
# This replaced the existing file sha-256-map.txt.

import os
import hashlib
import base64
import urllib

def nihash(str):
    result = base64.b64encode(hashlib.sha256(str).digest())
    result = result.replace("=", "")
    result = result.replace("/", "_")
    result = result.replace("+", "-")

    return result

def printHash(dirPath, dirNames, fileNames):
    for ignoreDir in [".git", ".svn"]:
        if ignoreDir in dirNames:
            # Prune this from the list of subdirectories to walk.
            dirNames.remove(ignoreDir)

    dirNames.sort()
    fileNames.sort()

    for fileName in fileNames:
        filePath = os.path.join(dirPath, fileName)
        file = open(filePath, "r")
        text = file.read()
        file.close()
    
	# Remove leading "./"
	if filePath[0:2] == "./":
	    filePath = filePath[2:]

        # Use urllib.quote to add %XX escapes for special characters needed for a URL.
        output.write(nihash(text) + " " + urllib.quote(filePath) + "\n")

output = open("sha-256-map.txt", "w") 
for (dirPath, dirNames, fileNames) in os.walk(".", True, None, True):
    printHash(dirPath, dirNames, fileNames)
output.close()

