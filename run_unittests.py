"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
#! coding: utf-8
import os
import shutil
import time
import subprocess

from os.path import abspath, dirname, basename, join, exists
from scripts import buildutils
from optparse import OptionParser


import xml.etree.ElementTree as ET
from xml.etree.ElementTree import ElementTree, Element

from xml.dom.minidom import getDOMImplementation

import zipfile

try:
    import build_config as CONFIG
except:
    import default_build_config as CONFIG


THISDIR = dirname(abspath(__file__))
VSTEST_CONSOLE = r'"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"'

RUNSETTINGS_TEMPLATE = """
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
    <MSPhoneTest>
        <TargetDevice>%(DEVICE_NAME)s</TargetDevice>
    </MSPhoneTest>
</RunSettings>
""".strip()

def call(*args, **kwargs):
    cmd = " ".join(args)
    print cmd
    try:
        return True, subprocess.check_output(cmd, **kwargs)
    except subprocess.CalledProcessError as e:
        return False, e.output.strip()

def get_application_id(options):
    """ Grab ProductID from manifest xml in the xap and cache it to options as appid attribute """
    if hasattr(options, "appid"): return options.appid

    zip_file = zipfile.ZipFile(options.xap, 'r')
    for fpath in zip_file.namelist():
        if fpath.endswith("WMAppManifest.xml"):
            manifest = zip_file.read(fpath)
            break
    else:
        raise Exception("No WMAppManifest.xml in XAP")

    zip_file.close()

    dom = ET.fromstring(manifest)#(manifestpath)
    appid = dom.find("App").get("ProductID")

    options.appid = appid
    return appid

def get_log(options):
    get_application_id(options)
    wputil(options, "get", "log.txt", "log.txt", "--app", options.appid )

def run_tests_old(options):
    appid = get_application_id(options)

    results = options.result.split(",")
    for result_file in results:
        if exists(result_file):
            os.remove(result_file)

    wputil(options, "uninstall", "--app", appid)

    wputil(options, "install", options.xap)
    wputil(options, "launch", "--app", appid)

    starttime = time.time()
    endtime = starttime + options.timeout
    try:
        while ( time.time() < endtime ):

            for result_file in results:
                try:
                    wputil(options, "get", result_file, result_file, "--app", appid )
                except subprocess.CalledProcessError:
                    break

                if not ( exists(result_file) and os.stat(result_file).st_size > 0 ):
                    break
            else:
                return results

            time.sleep(1)
    finally:
        # Always try to get the log
        get_log(options)

    raise Exception("Unit tests timed out");

def run_tests(options):
    runsettings = abspath("test.runsettings")
    with open(runsettings, 'w') as f:
        f.write( RUNSETTINGS_TEMPLATE % { "DEVICE_NAME" : options.device } )

    return call(options.vstest, options.xap, "/InIsolation", '/Settings:"%s"' % runsettings, shell=True, stderr = subprocess.STDOUT) # /InIsolation to suppress message

def check_results(results):
    success, junit = generate_junit_result(results)

    print junit
    with open("unittest-junit.xml", 'w') as f:
        f.write( junit )

    return success

def generate_junit_result(logdata):
    """ Generate results in JUnit format for Jenkins from the log data written by application

    <testsuite>
        <testcase classname="foo" name="ASuccessfulTest"/>
        <testcase classname="foo" name="AnotherSuccessfulTest"/>
        <testcase classname="foo" name="AFailingTest">
            <failure type="NotEnoughFoo"> details about failure </failure>
        </testcase>
    </testsuite>
    """

    lines = logdata.split("Starting test execution, please wait...")[-1].strip().split("\n")

    impl = getDOMImplementation()
    newdoc = impl.createDocument(None, "testsuite", None)
    top_element = newdoc.documentElement

    isexception = False
    success = True
    current_test = None
    errormsg = []

    def add_success():
        if current_test is not None:
            testclass, name = ("UnitTest", current_test)

            testcase = newdoc.createElement('testcase')
            testcase.setAttribute("classname", testclass)
            testcase.setAttribute("name", name)

            top_element.appendChild(testcase)

    def add_failure(current_test, errormsg):

        testclass, name = ( "UnitTest", current_test )

        testcase = newdoc.createElement('testcase')
        testcase.setAttribute("classname", testclass)
        testcase.setAttribute("name", name)

        failure = newdoc.createElement("failure")
        failure.setAttribute("Type", "Exception")

        for err in errormsg:
            errnode = newdoc.createTextNode(err)
            failure.appendChild(errnode)

        testcase.appendChild(failure)
        top_element.appendChild(testcase)

    for line in lines:
        if line.startswith("Total tests"): break

        if line.startswith("Passed") or line.startswith("Failed"):

            if len(errormsg):
                add_failure(current_test, errormsg)

            #if line.startswith("TestResult:"):
            passed = line.startswith("Passed")
            current_test = line.split(" ", 1)[-1].strip()

            if passed:
                add_success()

            errormsg = []
        elif len(line.strip()) > 0:
            errormsg.append(line)

    if len(errormsg):
        add_failure(current_test, errormsg)

    data = newdoc.toprettyxml(indent = "  ")
    # toprettyxml adds extra new lines
    lines = [ x for x in data.split("\n") if len(x.strip()) > 0]

    return success, "\n".join( lines )

def process(options):

    passed, data = run_tests(options)
    print data

    check_results(data)
    if not passed:
        raise Exception("tests failed!")

    #print "tests passed"

def create_options(parser):

    parser.add_option("-d", "--device",
                      default = CONFIG.Device,
                      help = "Name of the device to run the tests at.")

    parser.add_option("", "--wputil",
                      default = CONFIG.WPUtil,
                      help = "default:%default")

    parser.add_option("", "--vstest",
                      default = VSTEST_CONSOLE,
                      help = "default:%default")

    parser.add_option("", "--xap",
                      default = join( THISDIR, "src", "UnitTests", "Bin", "x86", "Debug", "UnitTests_Debug_x86.xap") ,
                      help = "Path to the unit test XAP. default:%default")

def main():
    parser = OptionParser( )

    create_options(parser)

    (options, args) = parser.parse_args()

    process(options)

if __name__ == "__main__":
    main()