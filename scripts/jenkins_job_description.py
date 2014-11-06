"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
"""
Tool to show build version information on the list of builds by 
reading it from WMAppManifest and printing it to console

The Jenkins job must have Post-Build Action::Set build description
* With Regular expression: JENKINS_VERSION_STRING: (.*)

"""

import buildutils
from os.path import basename, dirname, abspath, join

from xml.dom.minidom import parse, parseString

THISDIR = dirname(abspath(__file__))

def echo_version_for_jenkins(maj, min, build, branch):
    version = "%s.%s.%s" % (maj, min, build)
    print "BUILDINFO=Changes are in version %s(%s)" % (version, branch)
    print "JENKINS_VERSION_STRING: %s %s" % (branch, version)


def start():
    manifest_path = join(THISDIR, "..", "src", "MobileSecurity", "Properties", "WMAppManifest.xml")

    dom = parse(manifest_path)
    
    appdom = dom.getElementsByTagName("App")[0]

    version = appdom.getAttribute("Version")
    maj, min, build, rev = version.split(".")

    build += rev.zfill(2)

    print maj, min, build

    branch = buildutils.current_branch()

    echo_version_for_jenkins(maj, min, build, branch)

def main():
    start()
    
if __name__ == "__main__":
    main()