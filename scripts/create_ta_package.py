"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
import os
from os.path import join
from glob import glob
from zipfile import ZipFile, ZIP_DEFLATED
import base64


def createzip(name):
    with ZipFile(name, 'w', ZIP_DEFLATED) as myzip:

        def add(src):
            print src
            myzip.write(src)

        add("run_testautomation.py")
        add("default_build_config.py")

        for f in glob("tests/*.py"):
            add(f)

        for f in glob(join("src", "libs", "WPCommon", "FSTestAutomation", "python", "*.py")):
            add(f)

RUNNER_TEMPLATE = """
'''
Test automation runner for Safe Anywhere %(VERSION_INFO)s
'''
import os
import base64
import zipfile
import sys
import time

print "Application Version: %(VERSION_INFO)s"

TA_DIRECTORY = "test-automation-runner"
if not os.path.exists(TA_DIRECTORY):
    os.mkdir(TA_DIRECTORY)

ZIPDATA='''%(ZIPDATA)s'''

print "decoding archive"
data = base64.b64decode(ZIPDATA)

print "writing archive to file"
with open('tests.zip', 'wb') as f:
    f.write(data)

print "extracting archive"
with zipfile.ZipFile("tests.zip", 'r') as z:
    z.extractall()

print "running tests"
import run_testautomation
run_testautomation.main()

print "completed"

"""

def create_runner(name, embedded_zip, version):

    with open(embedded_zip, 'rb') as f:
        data = f.read()
        zipdata = base64.b64encode(data)

    with open(name, 'w') as f:
        data = RUNNER_TEMPLATE % {
            "ZIPDATA" : zipdata,
            "VERSION_INFO" : version
        }
        f.write(data)

    print name, "created"

def create(version, runnerpath):

    createzip('test-automation.zip')
    create_runner(runnerpath, 'test-automation.zip', version)
    os.remove('test-automation.zip')

if __name__ =="__main__":
    create("1.0", 'test-automation-runner.py')